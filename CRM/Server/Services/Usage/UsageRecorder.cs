using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRM.Server.Services.Usage
{
    /// <summary>
    /// Scrive il registro dei consumi.
    /// <para>
    /// Apre <b>un proprio scope</b> e quindi un proprio DbContext, mai quello del chiamante. Non e'
    /// un dettaglio di stile: scrivere su un contesto che ha appena fallito una SaveChanges fa
    /// ritentare le stesse modifiche gia' rifiutate, e l'errore che ne esce seppellisce quello
    /// vero. E' la trappola che in questo progetto ha gia' fatto sparire errori altrove.
    /// </para>
    /// </summary>
    public class UsageRecorder : IUsageRecorder
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IOptionsMonitor<AiPricingOptions> _pricing;
        private readonly ILogger<UsageRecorder> _logger;

        public UsageRecorder(
            IServiceScopeFactory scopeFactory,
            IHttpContextAccessor httpContextAccessor,
            IOptionsMonitor<AiPricingOptions> pricing,
            ILogger<UsageRecorder> logger)
        {
            _scopeFactory = scopeFactory;
            _httpContextAccessor = httpContextAccessor;
            _pricing = pricing;
            _logger = logger;
        }

        public Task RecordTokensAsync(
            ExternalServiceFeature feature,
            string? model,
            TokenUsage tokens,
            bool success,
            long elapsedMs)
        {
            // Una chiamata fallita prima di produrre qualunque token non e' costata niente:
            // registrarla gonfierebbe il numero di chiamate senza aggiungere spesa.
            if (tokens.IsEmpty && !success)
                return Task.CompletedTask;

            var pricing = _pricing.CurrentValue;

            return SaveAsync(new ExternalServiceUsage
            {
                OccurredAt = DateTime.UtcNow,
                Provider = ExternalServiceProvider.Anthropic,
                Feature = feature,
                Model = Truncate(model, 100),
                IdUser = CurrentUserId(),
                InputTokens = tokens.Input,
                OutputTokens = tokens.Output,
                CacheReadTokens = tokens.CacheRead,
                CacheWriteTokens = tokens.CacheWrite,
                Units = 1,
                EstimatedCost = AiCostCalculator.TokenCost(
                    pricing.FindModel(model), tokens.Input, tokens.Output, tokens.CacheRead, tokens.CacheWrite),
                Currency = pricing.Currency,
                Success = success,
                DurationMs = ToMs(elapsedMs),
            });
        }

        public Task RecordUnitsAsync(
            ExternalServiceProvider provider,
            ExternalServiceFeature feature,
            string operation,
            int units,
            bool success,
            long elapsedMs)
        {
            var pricing = _pricing.CurrentValue;

            return SaveAsync(new ExternalServiceUsage
            {
                OccurredAt = DateTime.UtcNow,
                Provider = provider,
                Feature = feature,
                Model = Truncate(operation, 100),
                IdUser = CurrentUserId(),
                Units = Math.Max(0, units),
                EstimatedCost = AiCostCalculator.UnitCost(pricing.FindOperation(operation), Math.Max(0, units)),
                Currency = pricing.Currency,
                Success = success,
                DurationMs = ToMs(elapsedMs),
            });
        }

        private async Task SaveAsync(ExternalServiceUsage usage)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                context.ExternalServiceUsages.Add(usage);

                // Deliberatamente senza token di annullamento: vedi IUsageRecorder.
                await context.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Il consumo e' gia' avvenuto e i soldi sono gia' spesi: perdere la riga e' un buco
                // nel rendiconto, non un motivo per far fallire la funzione che l'ha generata.
                _logger.LogWarning(ex, "Consumo {Feature} non registrato", usage.Feature);
            }
        }

        /// <summary>
        /// Nullo nei servizi in background (posta in arrivo, riassunti automatici): quella spesa
        /// non e' di nessun utente, e attribuirla a qualcuno sarebbe peggio che lasciarla vuota.
        /// </summary>
        private string? CurrentUserId()
            => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        private static int ToMs(long elapsedMs)
            => elapsedMs <= 0 ? 0 : (int)Math.Min(elapsedMs, int.MaxValue);

        private static string? Truncate(string? value, int max)
            => string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max);
    }
}
