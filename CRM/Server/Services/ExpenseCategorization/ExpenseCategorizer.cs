using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services.ExpenseCategorization
{
    /// <summary>
    /// Mette in fila i tre livelli e applica la soglia. La chiamata al modello sta dietro
    /// <see cref="IExpenseCategoryAiClient"/>: qui restano cascata, soglia e tempo massimo, cioe'
    /// la parte che deve restare prevedibile e verificabile senza rete.
    /// </summary>
    public class ExpenseCategorizer : IExpenseCategorizer
    {
        /// <summary>
        /// La classificazione avviene dentro la chiamata che elabora lo scontrino: oltre questo
        /// tempo si rinuncia e la tipologia resta da indicare, invece di far aspettare chi ha
        /// appena fotografato uno scontrino.
        /// </summary>
        private static readonly TimeSpan AiTimeout = TimeSpan.FromSeconds(20);

        private readonly ApplicationDbContext _context;
        private readonly IExpenseCategoryAiClient _ai;
        private readonly ILogger<ExpenseCategorizer> _logger;

        public ExpenseCategorizer(
            ApplicationDbContext context,
            IExpenseCategoryAiClient ai,
            ILogger<ExpenseCategorizer> logger)
        {
            _context = context;
            _ai = ai;
            _logger = logger;
        }

        public async Task<IReadOnlyList<ExpenseCategorySuggestion>> CategorizeAsync(
            IReadOnlyList<ExpenseCategoryRequest> requests,
            CancellationToken ct = default)
        {
            if (requests == null || requests.Count == 0)
                return Array.Empty<ExpenseCategorySuggestion>();

            var settings = await GetSettingsAsync(ct);

            // Livelli 1 e 2: sempre, per tutti. Non costano e non possono fallire.
            var suggestions = requests.Select(ExpenseCategoryRules.Apply).ToArray();

            // Livello 3 solo su quello che e' rimasto scoperto, e solo se qualcuno ha acceso
            // l'AI: il ripiego ha un costo a chiamata, le regole no.
            var uncovered = Enumerable.Range(0, suggestions.Length)
                .Where(index => !suggestions[index].HasCategory)
                .ToList();

            if (settings.AiEnabled && _ai.IsAvailable && uncovered.Count > 0)
            {
                var fromAi = await CallAiAsync(uncovered.Select(index => requests[index]).ToList(), ct);

                if (fromAi != null)
                {
                    for (var i = 0; i < uncovered.Count && i < fromAi.Count; i++)
                        suggestions[uncovered[i]] = fromAi[i];
                }
            }

            // La soglia si applica per ultima e a tutti i livelli: una tipologia proposta con poca
            // convinzione riempie un campo che nessuno riguardera' piu', ed e' proprio il campo da
            // cui dipende la deducibilita'.
            for (var i = 0; i < suggestions.Length; i++)
            {
                var suggestion = suggestions[i];

                if (!suggestion.HasCategory || suggestion.Confidence >= settings.MinConfidence)
                    continue;

                _logger.LogInformation(
                    "Tipologia {Category} scartata: confidenza {Confidence:P0} sotto la soglia {Threshold:P0}.",
                    suggestion.Category, suggestion.Confidence, settings.MinConfidence);

                suggestions[i] = ExpenseCategorySuggestion.None;
            }

            return suggestions;
        }

        private async Task<IReadOnlyList<ExpenseCategorySuggestion>?> CallAiAsync(
            IReadOnlyList<ExpenseCategoryRequest> requests, CancellationToken ct)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(AiTimeout);

            try
            {
                return await _ai.SuggestAsync(requests, timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Tipologia AI interrotta dopo {Seconds}s: le spese restano da classificare",
                    AiTimeout.TotalSeconds);
                return null;
            }
            catch (Exception ex)
            {
                // Nessun errore qui deve impedire di registrare una spesa.
                _logger.LogWarning(ex, "Tipologia AI non disponibile");
                return null;
            }
        }

        private async Task<(bool AiEnabled, double MinConfidence)> GetSettingsAsync(CancellationToken ct)
        {
            var settings = await _context.GlobalSettings
                .AsNoTracking()
                .OrderBy(g => g.Id)
                .Select(g => new { g.ExpenseCategoryAiEnabled, g.ExpenseCategoryMinConfidence })
                .FirstOrDefaultAsync(ct);

            if (settings == null)
                return (false, 0.6);

            // Una soglia fuori scala (riga vecchia, valore azzerato a mano) renderebbe muto o
            // ciarliero l'automatismo senza che si capisca perche'.
            var minConfidence = settings.ExpenseCategoryMinConfidence is > 0 and <= 1
                ? settings.ExpenseCategoryMinConfidence
                : 0.6;

            return (settings.ExpenseCategoryAiEnabled, minConfidence);
        }
    }
}
