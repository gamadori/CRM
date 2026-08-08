using System;
using System.Collections.Generic;
using System.Linq;

namespace CRM.Server.Services.Usage
{
    /// <summary>
    /// Prezzo di un modello, <b>per milione di token</b>: e' l'unita' in cui i fornitori
    /// pubblicano i listini, e tenerla uguale evita di sbagliare uno zero ricopiandoli.
    /// </summary>
    public class ModelPrice
    {
        public decimal Input { get; set; }

        public decimal Output { get; set; }

        /// <summary>Letture dalla cache. Se non dichiarato si applica un decimo dell'input.</summary>
        public decimal? CacheRead { get; set; }

        /// <summary>Scritture in cache. Se non dichiarato si applicano 1,25 volte l'input.</summary>
        public decimal? CacheWrite { get; set; }
    }

    /// <summary>
    /// Listino dei servizi esterni, dalla configurazione.
    /// <para>
    /// I prezzi <b>non</b> stanno nel codice: cambiano a ogni modello, e un listino sbagliato
    /// dentro una compilazione e' peggio di nessun listino, perche' produce un totale credibile
    /// e falso. Un modello che non compare qui non viene stimato: il costo resta nullo e si vede.
    /// </para>
    /// </summary>
    public class AiPricingOptions
    {
        public const string SectionName = "AiPricing";

        /// <summary>Valuta del listino. Viene congelata su ogni riga di consumo.</summary>
        public string Currency { get; set; } = "USD";

        /// <summary>Prezzo per milione di token, per id modello.</summary>
        public Dictionary<string, ModelPrice> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Prezzo per unita' dei servizi che non si pagano a token (una pagina OCR, un SMS).
        /// Chiave libera, la stessa che il chiamante registra come "operazione".
        /// </summary>
        public Dictionary<string, decimal> Operations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Trova il prezzo di un modello. Oltre alla corrispondenza esatta accetta il <b>prefisso
        /// piu' lungo</b> configurato: cosi' una voce "claude-haiku-4-5" copre anche l'id datato
        /// "claude-haiku-4-5-20251001", senza dover aggiornare il listino a ogni istantanea.
        /// </summary>
        public ModelPrice? FindModel(string? model)
        {
            if (string.IsNullOrWhiteSpace(model) || Models.Count == 0)
                return null;

            if (Models.TryGetValue(model, out var exact))
                return exact;

            return Models
                .Where(kv => model.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(kv => kv.Key.Length)
                .Select(kv => kv.Value)
                .FirstOrDefault();
        }

        public decimal? FindOperation(string? operation)
            => !string.IsNullOrWhiteSpace(operation) && Operations.TryGetValue(operation, out var price)
                ? price
                : null;
    }

    /// <summary>
    /// Conversione da consumo a importo. Statica e senza dipendenze di proposito: e' la parte
    /// che deve essere verificabile da un test senza tirarsi dietro database o configurazione.
    /// </summary>
    public static class AiCostCalculator
    {
        /// <summary>I listini sono espressi per milione di token.</summary>
        public const decimal TokensPerPriceUnit = 1_000_000m;

        private const decimal CacheReadRatio = 0.1m;
        private const decimal CacheWriteRatio = 1.25m;

        /// <summary>
        /// Costo di una chiamata a token. Nullo se il modello non e' a listino.
        /// <para>
        /// Quando il listino non dichiara i prezzi della cache si applicano i rapporti standard
        /// (un decimo dell'input in lettura, 1,25 volte in scrittura) invece di contarli zero:
        /// un consumo non prezzato e' un buco nel totale, non uno sconto.
        /// </para>
        /// </summary>
        public static decimal? TokenCost(ModelPrice? price, long inputTokens, long outputTokens, long cacheReadTokens, long cacheWriteTokens)
        {
            if (price == null)
                return null;

            var cacheReadPrice = price.CacheRead ?? price.Input * CacheReadRatio;
            var cacheWritePrice = price.CacheWrite ?? price.Input * CacheWriteRatio;

            var total =
                inputTokens * price.Input +
                outputTokens * price.Output +
                cacheReadTokens * cacheReadPrice +
                cacheWriteTokens * cacheWritePrice;

            return total / TokensPerPriceUnit;
        }

        /// <summary>Costo di un servizio a unita' (pagine, documenti). Nullo se l'operazione non e' a listino.</summary>
        public static decimal? UnitCost(decimal? unitPrice, int units)
            => unitPrice == null ? null : unitPrice.Value * units;
    }
}
