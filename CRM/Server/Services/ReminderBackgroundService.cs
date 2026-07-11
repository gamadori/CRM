using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Motore dei promemoria (Fase 3). Periodicamente cerca le attività con promemoria maturo
    /// e notifica l'assegnatario via push (fallback email).
    /// Gestisce uno stato di consegna esplicito (Pending → Sent | Failed): in caso di errore il
    /// promemoria resta in Failed e viene ritentato, con backoff, fino a <see cref="MaxRetries"/>
    /// tentativi prima di essere considerato definitivamente non consegnato.
    /// </summary>
    public class ReminderBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReminderBackgroundService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        /// <summary>Numero massimo di tentativi di consegna prima di rinunciare.</summary>
        private const int MaxRetries = 5;

        /// <summary>Attesa minima tra due tentativi sullo stesso promemoria (backoff).</summary>
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

        /// <summary>Lunghezza massima del messaggio d'errore salvato per diagnostica.</summary>
        private const int MaxErrorLength = 1000;

        public ReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ReminderBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Attende l'avvio completo dell'app prima del primo giro.
            try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); } catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ReminderBackgroundService: errore nel ciclo");
                }

                try { await Task.Delay(Interval, stoppingToken); } catch (TaskCanceledException) { break; }
            }
        }

        private async Task ProcessDueRemindersAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var push = sp.GetRequiredService<IPushNotificationService>();
            var email = sp.GetRequiredService<IEmailSenderPlus>();

            var now = DateTime.Now;
            var retryThreshold = now - RetryDelay;

            // Candidati: promemoria maturi su attività ancora pianificate che sono
            //   - mai tentati (Pending), oppure
            //   - falliti ma con tentativi residui e passato il backoff.
            var due = await db.Activities
                .Where(a => a.ReminderAt != null && a.ReminderAt <= now
                         && a.State == ActivityState.Planned
                         && (a.ReminderStatus == ReminderStatus.Pending
                             || (a.ReminderStatus == ReminderStatus.Failed
                                 && a.ReminderRetryCount < MaxRetries
                                 && (a.ReminderLastAttemptAt == null || a.ReminderLastAttemptAt <= retryThreshold))))
                .OrderBy(a => a.ReminderAt)
                .Take(50)
                .ToListAsync(ct);

            if (due.Count == 0)
                return;

            foreach (var activity in due)
            {
                // Registra il tentativo prima dell'invio: se il processo cade a metà,
                // il conteggio e l'istante restano coerenti e il backoff evita retry a raffica.
                activity.ReminderRetryCount++;
                activity.ReminderLastAttemptAt = DateTime.Now;

                // Destinatario: assegnatario, altrimenti creatore.
                var userId = string.IsNullOrWhiteSpace(activity.IdAssignee) ? activity.IdUser : activity.IdAssignee;
                if (string.IsNullOrWhiteSpace(userId))
                {
                    // Nessun destinatario: fallimento permanente, non ha senso ritentare.
                    activity.ReminderStatus = ReminderStatus.Failed;
                    activity.ReminderRetryCount = MaxRetries;
                    activity.ReminderLastError = "Nessun destinatario per il promemoria.";
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                var title = "Promemoria attività";
                var body = activity.DueDate != null
                    ? $"{activity.Subject} — scadenza {activity.DueDate:dd/MM/yyyy HH:mm}"
                    : activity.Subject;

                try
                {
                    var sent = await push.SendToUserAsync(userId, new { title, body, url = "/Agenda" });

                    // Fallback email se la push non ha raggiunto nessuna subscription.
                    if (sent <= 0)
                    {
                        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
                        if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                        {
                            await email.SendEmailAsync(new List<string> { user.Email }, EmailsTypes.NoticeReminder,
                                null, title, body, null);
                        }
                    }

                    // Consegna riuscita.
                    activity.ReminderStatus = ReminderStatus.Sent;
                    activity.ReminderLastError = null;
                }
                catch (Exception ex)
                {
                    // Consegna fallita: resta in Failed e verrà ritentato al prossimo giro
                    // (rispettando il backoff) finché non si esauriscono i tentativi.
                    activity.ReminderStatus = ReminderStatus.Failed;
                    activity.ReminderLastError = Truncate(ex.Message, MaxErrorLength);

                    var log = sp.GetRequiredService<ILogEventService>();
                    await log.RegisterAsync(nameof(ReminderBackgroundService), nameof(ProcessDueRemindersAsync), EventsTypes.Error, ex);
                }

                await db.SaveChangesAsync(ct);
            }
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
