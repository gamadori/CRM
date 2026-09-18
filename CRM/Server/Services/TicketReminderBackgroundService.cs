using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Scheduler dei preavvisi ticket. Cerca periodicamente i ticket maturi e delega
    /// destinatari/canali a <see cref="ITicketReminderNotificationService"/>.
    /// </summary>
    public class TicketReminderBackgroundService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

        private const int MaxRetries = 5;
        private const int MaxErrorLength = 1000;
        private const int MaxBatchSize = 100;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TicketReminderBackgroundService> _logger;

        public TicketReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<TicketReminderBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken); }
            catch (TaskCanceledException) { return; }

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

                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }
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

            var notifier = sp.GetRequiredService<ITicketReminderNotificationService>();
            var log = sp.GetRequiredService<ILogEventService>();

            var appointmentOverrides = await db.TicketTypes
                .Where(x => x.AppointmentReminderMinutes != null)
                .ToDictionaryAsync(x => x.Id, x => x.AppointmentReminderMinutes!.Value, ct);

            var expiryOverrides = await db.TicketTypes
                .Where(x => x.ExpiryReminderMinutes != null)
                .ToDictionaryAsync(x => x.Id, x => x.ExpiryReminderMinutes!.Value, ct);

            await ProcessAppointmentRemindersAsync(
                db,
                notifier,
                log,
                Math.Max(0, settings.TicketAppointmentReminderMinutes),
                appointmentOverrides,
                ct);

            await ProcessExpiryRemindersAsync(
                db,
                notifier,
                log,
                Math.Max(0, settings.TicketExpiryReminderMinutes),
                expiryOverrides,
                TicketExpirySchedule.FineGiornata(settings.OrarioDiLavoro()?.Fine),
                ct);
        }

        private async Task ProcessAppointmentRemindersAsync(
            ApplicationDbContext db,
            ITicketReminderNotificationService notifier,
            ILogEventService log,
            int globalMinutes,
            IReadOnlyDictionary<int, int> overrides,
            CancellationToken ct)
        {
            var now = DateTime.Now;
            var retryThreshold = now - RetryDelay;
            var maxMinutes = MaxReminderLead(globalMinutes, overrides);
            var upperDate = now.AddMinutes(maxMinutes);

            var candidates = await db.Tickets
                .Include(x => x.Company)
                .Where(x => !x.Closed
                         && x.Date != null
                         && x.Time != null
                         && x.Date <= upperDate
                         && (x.ReminderApptStatus == ReminderStatus.Pending
                             || (x.ReminderApptStatus == ReminderStatus.Failed
                                 && x.ReminderApptRetryCount < MaxRetries
                                 && (x.ReminderApptLastAttemptAt == null || x.ReminderApptLastAttemptAt <= retryThreshold))))
                .OrderBy(x => x.Date)
                .ThenBy(x => x.Time)
                .Take(MaxBatchSize)
                .ToListAsync(ct);

            foreach (var ticket in candidates)
            {
                var appointmentAt = ticket.Date!.Value.Date + ticket.Time!.Value.ToTimeSpan();
                var reminderAt = appointmentAt - TimeSpan.FromMinutes(EffectiveMinutes(ticket.IdType, globalMinutes, overrides));

                if (reminderAt > now)
                    continue;

                if (appointmentAt < now)
                {
                    ticket.ReminderApptStatus = ReminderStatus.Sent;
                    ticket.ReminderLastError = null;
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                ticket.ReminderApptRetryCount++;
                ticket.ReminderApptLastAttemptAt = DateTime.Now;

                var title = "Promemoria appuntamento ticket";
                var body = BuildBody($"Appuntamento {appointmentAt:dd/MM/yyyy HH:mm}", ticket);

                try
                {
                    var result = await notifier.NotifyAsync(new TicketReminderNotification
                    {
                        IdTicket = ticket.Id,
                        Kind = TicketReminderKind.Appointment,
                        Title = title,
                        Body = body,
                        Url = $"/Tickets/{ticket.Id}/Info"
                    }, ct);

                    if (!result.HasRecipients)
                    {
                        ResetAppointmentAttempt(ticket);
                        await db.SaveChangesAsync(ct);
                        continue;
                    }

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

        private async Task ProcessExpiryRemindersAsync(
            ApplicationDbContext db,
            ITicketReminderNotificationService notifier,
            ILogEventService log,
            int globalMinutes,
            IReadOnlyDictionary<int, int> overrides,
            TimeSpan fineGiornata,
            CancellationToken ct)
        {
            var now = DateTime.Now;
            var retryThreshold = now - RetryDelay;

            // Si pesca per GIORNO di scadenza, non per l'istante scritto nel campo: l'ora vera la
            // decide fineGiornata, e puo' cadere sia prima sia dopo quella memorizzata. Filtrando
            // sull'istante grezzo, un ticket scaduto alle 22:30 con chiusura alle 18:00 sarebbe
            // maturo quattro ore prima di entrare fra i candidati.
            var ultimoGiorno = now.AddMinutes(MaxReminderLead(globalMinutes, overrides)).Date.AddDays(1);

            var candidates = await db.Tickets
                .Include(x => x.Company)
                .Where(x => !x.Closed
                         && x.DateExpired != null
                         && x.DateExpired < ultimoGiorno
                         && (x.ReminderExpiryStatus == ReminderStatus.Pending
                             || (x.ReminderExpiryStatus == ReminderStatus.Failed
                                 && x.ReminderExpiryRetryCount < MaxRetries
                                 && (x.ReminderExpiryLastAttemptAt == null || x.ReminderExpiryLastAttemptAt <= retryThreshold))))
                .OrderBy(x => x.DateExpired)
                .Take(MaxBatchSize)
                .ToListAsync(ct);

            foreach (var ticket in candidates)
            {
                var minuti = EffectiveMinutes(ticket.IdType, globalMinutes, overrides);
                var scadenzaAt = TicketExpirySchedule.ScadenzaAt(ticket.DateExpired!.Value, fineGiornata);
                var reminderAt = TicketExpirySchedule.PreavvisoAt(ticket.DateExpired.Value, fineGiornata, minuti);

                if (reminderAt > now)
                    continue;

                ticket.ReminderExpiryRetryCount++;
                ticket.ReminderExpiryLastAttemptAt = DateTime.Now;

                var expired = scadenzaAt < now;
                var title = expired ? "Ticket scaduto non chiuso" : "Ticket in scadenza";

                // Solo il giorno: l'ora dentro DateExpired non l'ha scelta nessuno, e scriverla
                // nell'avviso significava annunciare una scadenza "alle 22:30" che in nessun'altra
                // schermata del programma esisteva.
                var when = expired
                    ? $"Scaduto il {ticket.DateExpired:dd/MM/yyyy}"
                    : $"Scadenza {ticket.DateExpired:dd/MM/yyyy}";
                var body = BuildBody(when, ticket);

                try
                {
                    var result = await notifier.NotifyAsync(new TicketReminderNotification
                    {
                        IdTicket = ticket.Id,
                        Kind = TicketReminderKind.Expiry,
                        Title = title,
                        Body = body,
                        Url = $"/Tickets/{ticket.Id}/Info",
                        TicketIsExpired = expired
                    }, ct);

                    if (!result.HasRecipients)
                    {
                        ResetExpiryAttempt(ticket);
                        await db.SaveChangesAsync(ct);
                        continue;
                    }

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

        private static int EffectiveMinutes(int idType, int globalMinutes, IReadOnlyDictionary<int, int> overrides)
            => overrides.TryGetValue(idType, out var minutes) ? Math.Max(0, minutes) : globalMinutes;

        private static int MaxReminderLead(int globalMinutes, IReadOnlyDictionary<int, int> overrides)
            => overrides.Count == 0 ? globalMinutes : Math.Max(globalMinutes, overrides.Values.Max());

        private static void ResetAppointmentAttempt(Ticket ticket)
        {
            ticket.ReminderApptRetryCount = Math.Max(0, ticket.ReminderApptRetryCount - 1);
            ticket.ReminderApptLastAttemptAt = null;
        }

        private static void ResetExpiryAttempt(Ticket ticket)
        {
            ticket.ReminderExpiryRetryCount = Math.Max(0, ticket.ReminderExpiryRetryCount - 1);
            ticket.ReminderExpiryLastAttemptAt = null;
        }

        private static string BuildBody(string when, Ticket ticket)
        {
            var company = ticket.Company?.RagioneSociale;
            var head = string.IsNullOrWhiteSpace(ticket.Numero) ? $"Ticket #{ticket.Id}" : $"Ticket {ticket.Numero}";
            return string.IsNullOrWhiteSpace(company)
                ? $"{head} - {when}"
                : $"{head} ({company}) - {when}";
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
