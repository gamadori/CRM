using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Si accorge delle macchine che hanno smesso di mandare il backup, e lo dice una volta al
    /// giorno con un riepilogo solo.
    /// <para>
    /// Un'email per macchina sembra piu' preciso ma non regge la realta': un guasto di rete zittisce
    /// venti macchine insieme, e venti email nello stesso minuto diventano venti email cestinate.
    /// Una sola, con l'elenco, si legge.
    /// </para>
    /// <para>
    /// Nasce spento. Senza giorni di soglia e senza destinatario non parte niente: accendere una
    /// sorveglianza che manda email deve deciderlo una persona, non un aggiornamento.
    /// </para>
    /// </summary>
    public class MachineBackupSilenceBackgroundService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        /// <summary>Ora in cui esce il riepilogo: la mattina, quando qualcuno lo legge.</summary>
        private const int OraDiInvio = 8;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MachineBackupSilenceBackgroundService> _logger;

        public MachineBackupSilenceBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<MachineBackupSilenceBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ControllaAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MachineBackupSilenceBackgroundService: errore nel ciclo");
                }

                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task ControllaAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();

            var settings = await db.GlobalSettings.FirstOrDefaultAsync(ct);
            if (settings == null || !settings.MachineBackupCheckEnabled)
                return;

            if (settings.MachineBackupSilenceDays <= 0)
                return;

            var destinatari = MachineBackupSilenceRules.Destinatari(settings.MachineBackupSilenceEmail);
            if (destinatari.Count == 0)
                return;

            var adesso = DateTime.Now;
            if (adesso.Hour < OraDiInvio)
                return;

            // Una volta al giorno, e la data sta in archivio: tenerla in memoria vorrebbe dire
            // rimandare il riepilogo a ogni riavvio del server.
            if (settings.MachineBackupSilenceLastSentOn?.Date >= adesso.Date)
                return;

            var candidate = await LeggiUltimiBackupAsync(db, ct);
            var inSilenzio = MachineBackupSilenceRules.InSilenzio(candidate, adesso, settings.MachineBackupSilenceDays);

            // Segnare la data anche quando tacciono tutte e nessuna e' in ritardo: il controllo di
            // oggi e' stato fatto, e senza segnarlo si ripeterebbe a ogni giro d'orologio.
            settings.MachineBackupSilenceLastSentOn = adesso;
            await db.SaveChangesAsync(ct);

            if (inSilenzio.Count == 0)
                return;

            var email = sp.GetRequiredService<IEmailSenderPlus>();
            var log = sp.GetRequiredService<ILogEventService>();

            var oggetto = $"Macchine senza backup: {inSilenzio.Count}";
            var testo = MachineBackupSilenceRules.Riepilogo(inSilenzio, settings.MachineBackupSilenceDays);

            // Un invio per destinatario, ognuno per conto suo: un indirizzo sbagliato in elenco non
            // deve impedire il riepilogo a chi c'e' scritto dopo.
            foreach (var destinatario in destinatari)
            {
                try
                {
                    await email.SendEmailAsync(destinatario, oggetto, testo);
                }
                catch (Exception ex)
                {
                    await log.RegisterAsync(nameof(MachineBackupSilenceBackgroundService), nameof(ControllaAsync), EventsTypes.Error, ex);
                }
            }
        }

        /// <summary>
        /// L'ultimo backup di ogni macchina che ne ha almeno uno. Il raggruppamento si fa in
        /// archivio: le macchine sono centinaia e i backup crescono a ogni caricamento.
        /// </summary>
        private static async Task<List<MacchinaInSilenzio>> LeggiUltimiBackupAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var ultimi = await db.MachineBackups.AsNoTracking()
                .Where(x => x.OwnerType == MachineBackupOwnerType.Article && x.IdArticle != null)
                .GroupBy(x => x.IdArticle!.Value)
                .Select(g => new { IdArticle = g.Key, Ultimo = g.Max(x => x.CreatedAt) })
                .ToListAsync(ct);

            if (ultimi.Count == 0)
                return new List<MacchinaInSilenzio>();

            var ids = ultimi.Select(x => x.IdArticle).ToList();
            var macchine = await db.Articles.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.SerialNumber,
                    ProductName = x.Product != null ? x.Product.Name : null,
                    CompanyName = x.Company != null ? x.Company.RagioneSociale : null
                })
                .ToListAsync(ct);

            return macchine.Join(ultimi, m => m.Id, u => u.IdArticle, (m, u) => new MacchinaInSilenzio
            {
                IdArticle = m.Id,
                SerialNumber = m.SerialNumber,
                ProductName = m.ProductName,
                CompanyName = m.CompanyName,
                UltimoBackup = u.Ultimo
            }).ToList();
        }
    }
}
