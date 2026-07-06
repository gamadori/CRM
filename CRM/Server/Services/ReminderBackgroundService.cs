using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Motore dei promemoria (Fase 3). Periodicamente cerca le attività con promemoria maturo
    /// e notifica l'assegnatario via push (fallback email). Idempotente: marca ReminderSent
    /// prima dell'invio per evitare doppioni. Non fa retry sui fallimenti di consegna.
    /// </summary>
    public class ReminderBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReminderBackgroundService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

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

            var due = await db.Activities
                .Where(a => !a.ReminderSent
                         && a.ReminderAt != null && a.ReminderAt <= now
                         && a.State == ActivityState.Planned)
                .OrderBy(a => a.ReminderAt)
                .Take(50)
                .ToListAsync(ct);

            if (due.Count == 0)
                return;

            foreach (var activity in due)
            {
                // 1) Marca subito (idempotenza): evita doppio invio se un tick si sovrappone.
                activity.ReminderSent = true;
                await db.SaveChangesAsync(ct);

                // 2) Notifica l'assegnatario (o il creatore se non assegnata).
                var userId = string.IsNullOrWhiteSpace(activity.IdAssignee) ? activity.IdUser : activity.IdAssignee;
                if (string.IsNullOrWhiteSpace(userId))
                    continue;

                var title = "Promemoria attività";
                var body = activity.DueDate != null
                    ? $"{activity.Subject} — scadenza {activity.DueDate:dd/MM/yyyy HH:mm}"
                    : activity.Subject;

                try
                {
                    var sent = await push.SendToUserAsync(userId, new { title, body, url = "/Agenda" });

                    // 3) Fallback email se la push non ha raggiunto nessuna subscription.
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
                catch (Exception ex)
                {
                    // Consegna fallita: il promemoria resta marcato come inviato (niente retry infinito), si logga.
                    var log = sp.GetRequiredService<ILogEventService>();
                    await log.RegisterAsync(nameof(ReminderBackgroundService), nameof(ProcessDueRemindersAsync), EventsTypes.Error, ex);
                }
            }
        }
    }
}
