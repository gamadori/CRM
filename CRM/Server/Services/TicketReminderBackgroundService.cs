using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Motore dei preavvisi dei ticket. Periodicamente cerca i ticket il cui preavviso è maturo
    /// e notifica gli assegnatari via push (fallback email). Sono gestiti due preavvisi indipendenti:
    ///  1) sull'appuntamento (Date + Time), con anticipo <see cref="GlobalSetting.TicketAppointmentReminderMinutes"/>;
    ///  2) sulla scadenza (DateExpired) se il ticket non è ancora chiuso, con anticipo
    ///     <see cref="GlobalSetting.TicketExpiryReminderMinutes"/>.
    /// I tempi di preavviso sono configurabili in GlobalSettings; l'intero motore è disattivabile
    /// con <see cref="GlobalSetting.TicketReminderEnabled"/>.
    /// Ogni preavviso ha uno stato di consegna esplicito (Pending → Sent | Failed) con retry e backoff.
    /// </summary>
    public class TicketReminderBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TicketReminderBackgroundService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        /// <summary>Numero massimo di tentativi di consegna prima di rinunciare.</summary>
        private const int MaxRetries = 5;

        /// <summary>Attesa minima tra due tentativi sullo stesso preavviso (backoff).</summary>
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

        /// <summary>Lunghezza massima del messaggio d'errore salvato per diagnostica.</summary>
        private const int MaxErrorLength = 1000;

        public TicketReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<TicketReminderBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Attende l'avvio completo dell'app prima del primo giro.
            try { await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken); } catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TicketReminderBackgroundService: errore nel ciclo");
                }

                try { await Task.Delay(Interval, stoppingToken); } catch (TaskCanceledException) { break; }
            }
        }

        private async Task ProcessDueRemindersAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();

            var settings = await db.GlobalSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new GlobalSetting();
            if (!settings.TicketReminderEnabled)
                return;

            var push = sp.GetRequiredService<IPushNotificationService>();
            var email = sp.GetRequiredService<IEmailSenderPlus>();
            var log = sp.GetRequiredService<ILogEventService>();

            // Override per Tipo Ticket: se valorizzati sostituiscono il default globale.
            var apptOverrides = await db.TicketTypes
                .Where(tt => tt.AppointmentReminderMinutes != null)
                .ToDictionaryAsync(tt => tt.Id, tt => tt.AppointmentReminderMinutes!.Value, ct);
            var expiryOverrides = await db.TicketTypes
                .Where(tt => tt.ExpiryReminderMinutes != null)
                .ToDictionaryAsync(tt => tt.Id, tt => tt.ExpiryReminderMinutes!.Value, ct);

            var globalApptMin = Math.Max(0, settings.TicketAppointmentReminderMinutes);
            var globalExpiryMin = Math.Max(0, settings.TicketExpiryReminderMinutes);

            await ProcessAppointmentRemindersAsync(db, push, email, log, globalApptMin, apptOverrides, ct);
            await ProcessExpiryRemindersAsync(db, push, email, log, globalExpiryMin, expiryOverrides, ct);
        }

        /// <summary>Anticipo effettivo (minuti) per un ticket: override del tipo se presente, altrimenti globale.</summary>
        private static int EffectiveMinutes(int idType, int globalMinutes, IReadOnlyDictionary<int, int> overrides)
            => overrides.TryGetValue(idType, out var m) ? Math.Max(0, m) : globalMinutes;

        // ─── Preavviso appuntamento (Date + Time) ────────────────────────────────
        private async Task ProcessAppointmentRemindersAsync(
            ApplicationDbContext db, IPushNotificationService push, IEmailSenderPlus email,
            ILogEventService log, int globalMinutes, IReadOnlyDictionary<int, int> overrides, CancellationToken ct)
        {
            var now = DateTime.Now;
            var retryThreshold = now - RetryDelay;
            // Limite grossolano sulla data col massimo anticipo possibile (globale o override);
            // il filtro preciso su Date+Time e sull'anticipo del singolo tipo è in memoria.
            var maxMinutes = overrides.Count > 0 ? Math.Max(globalMinutes, overrides.Values.Max()) : globalMinutes;
            var upperDate = now.AddMinutes(maxMinutes);

            var candidates = await db.Tickets
                .Include(t => t.Company)
                .Include(t => t.AssignedUsers)
                .Where(t => !t.Closed && t.Date != null && t.Time != null && t.Date <= upperDate
                         && (t.ReminderApptStatus == ReminderStatus.Pending
                             || (t.ReminderApptStatus == ReminderStatus.Failed
                                 && t.ReminderApptRetryCount < MaxRetries
                                 && (t.ReminderApptLastAttemptAt == null || t.ReminderApptLastAttemptAt <= retryThreshold))))
                .OrderBy(t => t.Date).ThenBy(t => t.Time)
                .Take(100)
                .ToListAsync(ct);

            foreach (var ticket in candidates)
            {
                var lead = TimeSpan.FromMinutes(EffectiveMinutes(ticket.IdType, globalMinutes, overrides));
                var apptDt = ticket.Date!.Value.Date + ticket.Time!.Value.ToTimeSpan();
                var reminderMoment = apptDt - lead;

                if (reminderMoment > now)
                    continue; // preavviso non ancora dovuto

                if (apptDt < now)
                {
                    // Appuntamento già passato (es. app spenta nel frattempo): nessun preavviso utile.
                    ticket.ReminderApptStatus = ReminderStatus.Sent;
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                var recipients = Recipients(ticket);
                if (recipients.Count == 0)
                    continue; // nessun assegnatario: riprova al prossimo giro (in attesa di assegnazione)

                ticket.ReminderApptRetryCount++;
                ticket.ReminderApptLastAttemptAt = DateTime.Now;

                var title = "Promemoria appuntamento ticket";
                var body = BuildBody($"Appuntamento {apptDt:dd/MM/yyyy HH:mm}", ticket);

                try
                {
                    await NotifyAsync(db, push, email, recipients, title, body, $"/Tickets/{ticket.Id}/Details", ct);
                    ticket.ReminderApptStatus = ReminderStatus.Sent;
                    ticket.ReminderLastError = null;
                }
                catch (Exception ex)
                {
                    ticket.ReminderApptStatus = ReminderStatus.Failed;
                    ticket.ReminderLastError = Truncate(ex.Message, MaxErrorLength);
                    await log.RegisterAsync(nameof(TicketReminderBackgroundService), nameof(ProcessAppointmentRemindersAsync), EventsTypes.Error, ex);
                }

                await db.SaveChangesAsync(ct);
            }
        }

        // ─── Preavviso scadenza (DateExpired, solo se non chiuso) ─────────────────
        private async Task ProcessExpiryRemindersAsync(
            ApplicationDbContext db, IPushNotificationService push, IEmailSenderPlus email,
            ILogEventService log, int globalMinutes, IReadOnlyDictionary<int, int> overrides, CancellationToken ct)
        {
            var now = DateTime.Now;
            var retryThreshold = now - RetryDelay;
            var maxMinutes = overrides.Count > 0 ? Math.Max(globalMinutes, overrides.Values.Max()) : globalMinutes;
            var upperBound = now.AddMinutes(maxMinutes);

            var candidates = await db.Tickets
                .Include(t => t.Company)
                .Include(t => t.AssignedUsers)
                .Where(t => !t.Closed && t.DateExpired != null && t.DateExpired <= upperBound
                         && (t.ReminderExpiryStatus == ReminderStatus.Pending
                             || (t.ReminderExpiryStatus == ReminderStatus.Failed
                                 && t.ReminderExpiryRetryCount < MaxRetries
                                 && (t.ReminderExpiryLastAttemptAt == null || t.ReminderExpiryLastAttemptAt <= retryThreshold))))
                .OrderBy(t => t.DateExpired)
                .Take(100)
                .ToListAsync(ct);

            foreach (var ticket in candidates)
            {
                // Anticipo effettivo del tipo: un ticket ripescato dal bound massimo potrebbe
                // non essere ancora "maturo" per il suo anticipo specifico → si salta.
                var lead = TimeSpan.FromMinutes(EffectiveMinutes(ticket.IdType, globalMinutes, overrides));
                if (ticket.DateExpired!.Value - lead > now)
                    continue;

                var recipients = Recipients(ticket);
                if (recipients.Count == 0)
                    continue; // nessun assegnatario: riprova al prossimo giro

                ticket.ReminderExpiryRetryCount++;
                ticket.ReminderExpiryLastAttemptAt = DateTime.Now;

                var expired = ticket.DateExpired!.Value < now;
                var title = expired ? "Ticket scaduto non chiuso" : "Ticket in scadenza";
                var when = expired ? $"Scaduto il {ticket.DateExpired:dd/MM/yyyy HH:mm}" : $"Scadenza {ticket.DateExpired:dd/MM/yyyy HH:mm}";
                var body = BuildBody(when, ticket);

                try
                {
                    await NotifyAsync(db, push, email, recipients, title, body, $"/Tickets/{ticket.Id}/Details", ct);
                    ticket.ReminderExpiryStatus = ReminderStatus.Sent;
                    ticket.ReminderLastError = null;
                }
                catch (Exception ex)
                {
                    ticket.ReminderExpiryStatus = ReminderStatus.Failed;
                    ticket.ReminderLastError = Truncate(ex.Message, MaxErrorLength);
                    await log.RegisterAsync(nameof(TicketReminderBackgroundService), nameof(ProcessExpiryRemindersAsync), EventsTypes.Error, ex);
                }

                await db.SaveChangesAsync(ct);
            }
        }

        /// <summary>Utenti assegnati al ticket (collezione multipla + campo legacy), distinti.</summary>
        private static List<string> Recipients(Ticket ticket)
        {
            var ids = ticket.AssignedUsers?
                .Select(a => a.IdUser)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList() ?? new List<string>();

            if (!string.IsNullOrWhiteSpace(ticket.IdUserAssigned) && !ids.Contains(ticket.IdUserAssigned))
                ids.Add(ticket.IdUserAssigned);

            return ids.Distinct().ToList();
        }

        /// <summary>Invia la notifica push a ciascun destinatario, con fallback email se la push non arriva.</summary>
        private static async Task NotifyAsync(
            ApplicationDbContext db, IPushNotificationService push, IEmailSenderPlus email,
            List<string> userIds, string title, string body, string url, CancellationToken ct)
        {
            foreach (var userId in userIds)
            {
                var sent = await push.SendToUserAsync(userId, new { title, body, url });

                if (sent <= 0)
                {
                    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
                    if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                    {
                        await email.SendEmailAsync(new List<string> { user.Email }, EmailsTypes.NoticeReminder,
                            null, title, body, null);
                    }
                }
            }
        }

        private static string BuildBody(string when, Ticket ticket)
        {
            var company = ticket.Company?.RagioneSociale;
            var head = string.IsNullOrWhiteSpace(ticket.Numero) ? $"Ticket #{ticket.Id}" : $"Ticket {ticket.Numero}";
            return string.IsNullOrWhiteSpace(company)
                ? $"{head} — {when}"
                : $"{head} ({company}) — {when}";
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
