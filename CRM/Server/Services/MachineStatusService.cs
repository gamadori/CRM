using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    public interface IMachineStatusService
    {
        Task<MachineStatusResponse> ApplyAsync(Article article, MachineStatusRequest request, string source, CancellationToken ct = default);

        Task<MachineStatusOverviewDTO> GetOverviewAsync(int idArticle, int days, CancellationToken ct = default);
    }

    /// <summary>
    /// Applica la fotografia che una macchina manda di se stessa.
    /// <para>
    /// Il lavoro vero e' il <b>confronto</b>: la macchina dice com'e' fatta adesso, e qui si guarda
    /// cosa e' cambiato rispetto all'ultima volta. Gli eventi di cambio versione li scrive il CRM,
    /// non li dichiara la macchina - cosi' un aggiornamento fatto con la rete giu' viene recuperato
    /// al primo collegamento, invece di lasciare in archivio una versione che non esiste piu'.
    /// </para>
    /// </summary>
    public class MachineStatusService : IMachineStatusService
    {
        private readonly ApplicationDbContext _context;

        public MachineStatusService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MachineStatusResponse> ApplyAsync(
            Article article,
            MachineStatusRequest request,
            string source,
            CancellationToken ct = default)
        {
            var risposta = new MachineStatusResponse { SerialNumber = article.SerialNumber };
            var adesso = DateTime.UtcNow;

            if (request.Components is { Count: > 0 })
                await ApplyComponentsAsync(article.Id, request.Components, source, adesso, risposta, ct);

            if (request.Counters != null)
                risposta.Counters = await ApplyCountersAsync(article.Id, request.Counters, adesso, ct);

            await _context.SaveChangesAsync(ct);

            return risposta;
        }

        /// <summary>
        /// Cosa mostrare nella scheda della macchina: i componenti con la versione di adesso, gli
        /// ultimi cambi, e le giornate di lavoro del periodo scelto.
        /// </summary>
        public async Task<MachineStatusOverviewDTO> GetOverviewAsync(int idArticle, int days, CancellationToken ct = default)
        {
            var giorni = Math.Clamp(days, 1, 365);
            var da = DateTime.Now.Date.AddDays(-giorni);

            var componenti = await _context.MachineComponents.AsNoTracking()
                .Where(x => x.IdArticle == idArticle)
                .OrderBy(x => x.Kind).ThenBy(x => x.Code)
                .Select(x => new MachineComponentDTO
                {
                    Id = x.Id,
                    Code = x.Code,
                    Kind = x.Kind,
                    Name = x.Name,
                    CurrentVersion = x.CurrentVersion,
                    SerialNumber = x.SerialNumber,
                    LastSeenAt = x.LastSeenAt,
                    LastVersionChangeAt = x.LastVersionChangeAt
                })
                .ToListAsync(ct);

            // I cambi si guardano su TUTTA la storia, non solo sul periodo dei contatori: un
            // aggiornamento firmware di due anni fa e' esattamente cio' che si cerca quando una
            // macchina comincia a dare problemi.
            var cambi = await _context.MachineComponentVersionChanges.AsNoTracking()
                .Where(x => x.Component!.IdArticle == idArticle)
                .OrderByDescending(x => x.DetectedAt)
                .Take(50)
                .Select(x => new MachineVersionChangeLogDTO
                {
                    Code = x.Component!.Code,
                    Kind = x.Component.Kind,
                    FromVersion = x.FromVersion,
                    ToVersion = x.ToVersion,
                    DetectedAt = x.DetectedAt,
                    Source = x.Source
                })
                .ToListAsync(ct);

            var letture = await _context.MachineDailyReadings.AsNoTracking()
                .Where(x => x.IdArticle == idArticle && x.Day >= da)
                .OrderByDescending(x => x.Day)
                .Select(x => new MachineDailyReadingDTO
                {
                    Day = x.Day,
                    TotalHours = x.TotalHours,
                    TotalPieces = x.TotalPieces,
                    Hours = x.Hours,
                    Pieces = x.Pieces,
                    CounterReset = x.CounterReset
                })
                .ToListAsync(ct);

            return new MachineStatusOverviewDTO
            {
                Components = componenti,
                RecentChanges = cambi,
                Readings = letture,

                // I giorni con contatore azzerato restano fuori dal totale: quel valore e' il
                // conteggio ripartito da zero, non il lavoro di quella giornata.
                PeriodHours = letture.Where(x => !x.CounterReset).Sum(x => x.Hours),
                PeriodPieces = letture.Where(x => !x.CounterReset).Sum(x => x.Pieces)
            };
        }

        private async Task ApplyComponentsAsync(
            int idArticle,
            List<MachineComponentStatus> componenti,
            string source,
            DateTime adesso,
            MachineStatusResponse risposta,
            CancellationToken ct)
        {
            var codici = componenti
                .Select(x => x.Code?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();

            var esistenti = await _context.MachineComponents
                .Where(x => x.IdArticle == idArticle && codici.Contains(x.Code))
                .ToDictionaryAsync(x => x.Code, ct);

            foreach (var arrivato in componenti)
            {
                var code = arrivato.Code?.Trim();
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                risposta.ComponentsReceived++;
                var versione = Normalizza(arrivato.Version);

                if (!esistenti.TryGetValue(code, out var componente))
                {
                    componente = new MachineComponent
                    {
                        IdArticle = idArticle,
                        Code = code,
                        Kind = arrivato.Kind,
                        Name = Normalizza(arrivato.Name),
                        CurrentVersion = versione,
                        SerialNumber = Normalizza(arrivato.SerialNumber),
                        FirstSeenAt = adesso,
                        LastSeenAt = adesso,
                        LastVersionChangeAt = versione == null ? null : adesso
                    };

                    _context.MachineComponents.Add(componente);
                    esistenti[code] = componente;
                    risposta.NewComponents.Add(code);

                    // Anche la prima versione e' un cambio: senza, la storia di un componente
                    // comincerebbe dal suo primo aggiornamento e non da com'era quando e' arrivato.
                    if (versione != null)
                        RegistraCambio(componente, null, versione, source, adesso, risposta);

                    continue;
                }

                componente.LastSeenAt = adesso;
                componente.Kind = arrivato.Kind;

                if (Normalizza(arrivato.Name) is { } nome)
                    componente.Name = nome;

                if (Normalizza(arrivato.SerialNumber) is { } matricola)
                    componente.SerialNumber = matricola;

                // Versione assente nella fotografia: la macchina non l'ha detta, non vuol dire che
                // il componente l'abbia persa. Cancellarla sarebbe inventare un cambio.
                if (versione == null || versione == componente.CurrentVersion)
                    continue;

                RegistraCambio(componente, componente.CurrentVersion, versione, source, adesso, risposta);
                componente.CurrentVersion = versione;
                componente.LastVersionChangeAt = adesso;
            }
        }

        private void RegistraCambio(
            MachineComponent componente,
            string? da,
            string? a,
            string source,
            DateTime adesso,
            MachineStatusResponse risposta)
        {
            _context.MachineComponentVersionChanges.Add(new MachineComponentVersionChange
            {
                Component = componente,
                FromVersion = da,
                ToVersion = a,
                DetectedAt = adesso,
                Source = source
            });

            risposta.VersionChanges.Add(new MachineVersionChangeDTO
            {
                Code = componente.Code,
                FromVersion = da,
                ToVersion = a
            });
        }

        /// <summary>
        /// Registra i totalizzatori del giorno e ne calcola la differenza con la lettura precedente.
        /// Rimandare lo stesso giorno aggiorna quella lettura invece di crearne una seconda: due
        /// letture dello stesso giorno sarebbero la stessa produzione contata due volte.
        /// </summary>
        private async Task<MachineDailyReadingDTO> ApplyCountersAsync(
            int idArticle,
            MachineCountersStatus counters,
            DateTime adesso,
            CancellationToken ct)
        {
            var giorno = (counters.Day ?? DateTime.Now).Date;

            var lettura = await _context.MachineDailyReadings
                .FirstOrDefaultAsync(x => x.IdArticle == idArticle && x.Day == giorno, ct);

            if (lettura == null)
            {
                lettura = new MachineDailyReading { IdArticle = idArticle, Day = giorno };
                _context.MachineDailyReadings.Add(lettura);
            }

            lettura.TotalHours = counters.TotalHours;
            lettura.TotalPieces = counters.TotalPieces;
            lettura.ReceivedAt = adesso;

            // La lettura buona per il confronto e' quella del giorno precedente piu' vicino, non
            // "quella di ieri": una macchina ferma per ferie non azzera il conto alla ripartenza.
            var precedente = await _context.MachineDailyReadings
                .Where(x => x.IdArticle == idArticle && x.Day < giorno)
                .OrderByDescending(x => x.Day)
                .FirstOrDefaultAsync(ct);

            lettura.Hours = Differenza(precedente?.TotalHours, counters.TotalHours, out var oreScese);
            lettura.Pieces = Differenza(precedente?.TotalPieces, counters.TotalPieces, out var pezziScesi);
            lettura.CounterReset = oreScese || pezziScesi;

            return new MachineDailyReadingDTO
            {
                Day = lettura.Day,
                TotalHours = lettura.TotalHours,
                TotalPieces = lettura.TotalPieces,
                Hours = lettura.Hours,
                Pieces = lettura.Pieces,
                CounterReset = lettura.CounterReset
            };
        }

        /// <summary>
        /// Quanto ha fatto in quel giorno. Senza una lettura precedente non c'e' differenza da
        /// calcolare, e il primo giorno resta vuoto: scriverci il totalizzatore vorrebbe dire
        /// dichiarare che la macchina ha prodotto in un giorno tutto cio' che ha prodotto in dieci
        /// anni. Se il totale <b>scende</b> il contatore e' stato azzerato (scheda sostituita,
        /// assistenza): si prende il valore attuale come lavoro del giorno e si segna il fatto.
        /// </summary>
        private static decimal? Differenza(decimal? precedente, decimal? attuale, out bool azzerato)
        {
            azzerato = false;

            if (attuale == null)
                return null;

            if (precedente == null)
                return null;

            if (attuale < precedente)
            {
                azzerato = true;
                return attuale;
            }

            return attuale - precedente;
        }

        private static long? Differenza(long? precedente, long? attuale, out bool azzerato)
        {
            azzerato = false;

            if (attuale == null || precedente == null)
                return null;

            if (attuale < precedente)
            {
                azzerato = true;
                return attuale;
            }

            return attuale - precedente;
        }

        private static string? Normalizza(string? valore)
            => string.IsNullOrWhiteSpace(valore) ? null : valore.Trim();
    }
}
