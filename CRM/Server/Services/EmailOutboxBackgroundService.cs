using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services.Email;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Motore di trasmissione della coda email (outbox pattern). Periodicamente preleva i messaggi
    /// accodati da <see cref="EmailService"/> e li invia, con una macchina a stati di consegna
    /// esplicita (Pending → Sent | Failed) modellata come quella dei promemoria
    /// (<see cref="ReminderBackgroundService"/>): in caso di errore la riga resta Failed e viene
    /// ritentata, con backoff, fino a <see cref="MaxRetries"/> tentativi.
    /// La trasmissione usa una <b>catena di canali con failover</b> (primario → fallback): se un
    /// canale è indisponibile si passa al successivo, così un disservizio del provider non blocca
    /// le email. A invio riuscito registra l'email nel log <see cref="EmailSent"/> e, se presente un
    /// aggancio CRM, la comunicazione nella timeline Attività dell'entità collegata.
    /// </summary>
    public class EmailOutboxBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailOutboxBackgroundService> _logger;
        private readonly IHttpClientFactory _httpFactory;

        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        /// <summary>Numero massimo di tentativi di invio prima di rinunciare.</summary>
        private const int MaxRetries = 5;

        /// <summary>Attesa minima tra due tentativi sullo stesso messaggio (backoff).</summary>
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(2);

        /// <summary>Numero massimo di messaggi elaborati per ciclo.</summary>
        private const int BatchSize = 50;

        /// <summary>Lunghezza massima del messaggio d'errore salvato per diagnostica.</summary>
        private const int MaxErrorLength = 1000;

        public EmailOutboxBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<EmailOutboxBackgroundService> logger,
            IHttpClientFactory httpFactory)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _httpFactory = httpFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Attende l'avvio completo dell'app prima del primo giro.
            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); } catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "EmailOutboxBackgroundService: errore nel ciclo");
                }

                try { await Task.Delay(Interval, stoppingToken); } catch (TaskCanceledException) { break; }
            }
        }

        private async Task ProcessPendingAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.Now;
            var retryThreshold = now - RetryDelay;

            // Candidati: mai tentati (Pending), oppure falliti con tentativi residui e backoff scaduto.
            var due = await db.EmailsOutbox
                .Where(o => o.Status == EmailOutboxStatus.Pending
                         || (o.Status == EmailOutboxStatus.Failed
                             && o.RetryCount < MaxRetries
                             && (o.LastAttemptAt == null || o.LastAttemptAt <= retryThreshold)))
                .OrderBy(o => o.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (due.Count == 0)
                return;

            var chain = await BuildTransmitterChainAsync(db, ct);
            if (chain.Count == 0)
            {
                _logger.LogWarning("EmailOutboxBackgroundService: nessun canale di invio configurato, {Count} email in coda", due.Count);
                return;
            }

            // MailKit valida i certificati tramite questo callback globale se non impostato sul client.
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (s, c, chainCert, e) => true;

            foreach (var outbox in due)
            {
                // Registra il tentativo prima dell'invio: se il processo cade a metà, il conteggio e
                // l'istante restano coerenti e il backoff evita retry a raffica.
                outbox.RetryCount++;
                outbox.LastAttemptAt = DateTime.Now;

                await TrySendAsync(db, sp, chain, outbox, ct);

                await db.SaveChangesAsync(ct);
            }
        }

        /// <summary>
        /// Prova a trasmettere il messaggio percorrendo la catena di canali in ordine: si ferma al
        /// primo successo, altrimenti aggrega gli errori e lascia la riga in Failed per il retry.
        /// </summary>
        private async Task TrySendAsync(ApplicationDbContext db, IServiceProvider sp, IReadOnlyList<IEmailTransmitter> chain, EmailOutbox outbox, CancellationToken ct)
        {
            MimeMessage message;
            try
            {
                using var stream = new MemoryStream(outbox.Payload);
                message = await MimeMessage.LoadAsync(stream, ct);
            }
            catch (Exception ex)
            {
                // Payload corrotto: nessun canale potrà mai inviarlo, inutile ritentare.
                outbox.Status = EmailOutboxStatus.Failed;
                outbox.RetryCount = MaxRetries;
                outbox.LastError = Truncate("Payload non leggibile: " + ex.Message, MaxErrorLength);
                await LogAsync(sp, ex);
                return;
            }

            var errors = new List<string>();

            // Identificatore di correlazione per il tracking di engagement (Tier 3): agganciato
            // all'invio dai canali ESP e restituito nei loro webhook.
            var messageRef = Guid.NewGuid().ToString("N");

            foreach (var transmitter in chain)
            {
                try
                {
                    var result = await transmitter.SendAsync(message, messageRef, ct);

                    outbox.Status = EmailOutboxStatus.Sent;
                    outbox.SentAt = DateTime.Now;
                    outbox.LastError = null;

                    RecordSent(db, outbox, message, transmitter.Name, result, messageRef);
                    return;
                }
                catch (Exception ex)
                {
                    errors.Add($"{transmitter.Name}: {ex.Message}");
                    await LogAsync(sp, ex);
                }
            }

            // Tutti i canali hanno fallito: resta Failed, verrà ritentato al prossimo giro.
            outbox.Status = EmailOutboxStatus.Failed;
            outbox.LastError = Truncate(string.Join(" | ", errors), MaxErrorLength);
        }

        /// <summary>
        /// Costruisce la catena di trasmettitori dai canali configurati in DB (tabella SmtpSettings),
        /// solo quelli attivi, in ordine di priorità. Ogni riga è mappata sul trasmettitore del suo
        /// tipo (SMTP o provider API come Brevo); le righe incomplete vengono ignorate.
        /// </summary>
        private async Task<IReadOnlyList<IEmailTransmitter>> BuildTransmitterChainAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var channels = await db.SmtpSettings
                .Where(s => s.IsActive)
                .OrderBy(s => s.Priority)
                .ThenBy(s => s.Id)
                .ToListAsync(ct);

            var chain = new List<IEmailTransmitter>();

            foreach (var channel in channels)
            {
                IEmailTransmitter? transmitter = channel.Provider switch
                {
                    EmailProvider.Smtp when !string.IsNullOrWhiteSpace(channel.Server) =>
                        new SmtpEmailTransmitter(channel.DisplayName, channel.Server, channel.Port, channel.Ssl, channel.Username, channel.Password),

                    EmailProvider.Brevo when !string.IsNullOrWhiteSpace(channel.ApiKey) =>
                        new BrevoEmailTransmitter(channel.DisplayName, _httpFactory, channel.ApiKey!, channel.SenderEmail, channel.SenderName),

                    EmailProvider.SendGrid when !string.IsNullOrWhiteSpace(channel.ApiKey) =>
                        new SendGridEmailTransmitter(channel.DisplayName, channel.ApiKey!, channel.SenderEmail, channel.SenderName),

                    _ => null
                };

                if (transmitter != null)
                    chain.Add(transmitter);
            }

            return chain;
        }

        /// <summary>
        /// Registra l'esito riuscito: scrive il log <see cref="EmailSent"/> (con il canale usato) e,
        /// se l'email è agganciata a un'entità CRM, crea l'attività Email nella sua timeline
        /// (collegata all'EmailSent). Le entità vengono solo aggiunte al contesto: la persistenza
        /// avviene nel SaveChanges della riga di outbox, così stato e registrazioni sono atomici.
        /// </summary>
        private static void RecordSent(ApplicationDbContext db, EmailOutbox outbox, MimeMessage message, string via, string result, string messageRef)
        {
            var now = DateTime.Now;

            var emailSent = new EmailSent
            {
                DateSent = now,
                Subject = outbox.Subject,
                IdUser = outbox.IdUser,
                Message = message.HtmlBody ?? message.TextBody ?? string.Empty,
                Result = $"[{via}] {result}",
                To = outbox.To,
                CC = outbox.Cc ?? string.Empty,
                Attchments = outbox.Attachments,
                MessageRef = messageRef,
                EngagementStatus = EmailEngagementStatus.Sent
            };
            db.EmailsSent.Add(emailSent);

            if (outbox.EntityType != null && outbox.EntityId != null)
            {
                db.Activities.Add(new Activity
                {
                    Kind = ActivityKind.Email,
                    Subject = string.IsNullOrWhiteSpace(outbox.Subject) ? "Email inviata" : outbox.Subject,
                    EntityType = outbox.EntityType.Value,
                    EntityId = outbox.EntityId.Value,
                    IdUser = outbox.IdUser,
                    State = ActivityState.Done,
                    DoneDate = now,
                    CreatedAt = now,
                    EmailSent = emailSent
                });
            }
        }

        private static async Task LogAsync(IServiceProvider sp, Exception ex)
        {
            var log = sp.GetRequiredService<ILogEventService>();
            await log.RegisterAsync(nameof(EmailOutboxBackgroundService), nameof(ProcessPendingAsync), EventsTypes.Error, ex);
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
