using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CRM.Server.Services
{
    /// <summary>
    /// Che valuta c'e' <b>scritta</b> su un documento: simboli, codici ISO e l'indizio del paese
    /// dell'esercente.
    /// <para>
    /// Sono funzioni pure su stringhe, tenute fuori dall'analizzatore apposta: cosi' si provano sul
    /// testo di documenti veri senza chiamare Azure. Il caso che ha portato a estrarle e' un conto
    /// d'albergo di Redmond, WA in cui la valuta compare due volte ("Rate: $127.00" e
    /// "Balance 0.00 USD") e nessuna delle due dentro i campi degli importi, per cui il documento
    /// risultava "valuta non rilevata" pur avendola stampata sopra.
    /// </para>
    /// </summary>
    public static class ReceiptCurrencyText
    {
        /// <summary>
        /// Simboli che identificano una valuta senza ambiguita': qui si puo' concludere da soli.
        /// </summary>
        public static readonly Dictionary<string, string> UnambiguousSymbols = new()
        {
            ["€"] = "EUR",
            ["£"] = "GBP",
            ["₣"] = "CHF",
            ["₹"] = "INR",
            ["₽"] = "RUB",
            ["₩"] = "KRW",
            ["₪"] = "ILS",
            ["₺"] = "TRY",
            ["zł"] = "PLN",
            ["Kč"] = "CZK",
        };

        /// <summary>
        /// Simboli usati da piu' valute, con i candidati in ordine di probabilita'.
        /// <para>
        /// Non si sceglie per conto dell'operatore - una valuta sbagliata falsa la conversione e
        /// quindi il rimborso - ma non si butta via nemmeno l'informazione: il simbolo sullo
        /// scontrino c'e', e lasciare il campo vuoto dicendo "valuta non rilevata" mentre il "$"
        /// e' bello visibile e' un modo di dare torto a chi guarda.
        /// </para>
        /// </summary>
        public static readonly Dictionary<string, string[]> AmbiguousSymbols = new()
        {
            ["$"] = new[] { "USD", "CAD", "AUD", "NZD", "SGD", "HKD", "MXN" },
            ["¥"] = new[] { "JPY", "CNY" },
            ["kr"] = new[] { "SEK", "NOK", "DKK" },
            ["R$"] = new[] { "BRL" },
            ["₨"] = new[] { "PKR", "LKR", "NPR" },
        };

        /// <summary>
        /// Indizi nell'indirizzo dell'esercente. Non decidono: propongono un candidato che chi
        /// registra conferma.
        /// </summary>
        public static readonly (string Pattern, string Currency)[] AddressHints =
        {
            (@"\b(USA|U\.S\.A\.|UNITED STATES)\b", "USD"),
            // Sigla di stato USA di due lettere seguita dal CAP a 5 cifre: "Redmond, WA 98052".
            (@"\b[A-Z]{2}\s+\d{5}(-\d{4})?\b", "USD"),
            (@"\b(CANADA)\b", "CAD"),
            (@"\b(AUSTRALIA)\b", "AUD"),
            (@"\b(SINGAPORE)\b", "SGD"),
            (@"\b(HONG KONG)\b", "HKD"),
            (@"\b(JAPAN|NIPPON)\b", "JPY"),
            (@"\b(CHINA)\b", "CNY"),
        };

        /// <summary>
        /// Codici accettati quando compaiono nel testo. Sono quelli delle due tabelle dei simboli
        /// piu' le valute senza un simbolo proprio, che nei documenti si scrivono sempre cosi'.
        /// </summary>
        public static readonly HashSet<string> KnownCodes = new(StringComparer.Ordinal)
        {
            "EUR", "USD", "GBP", "CHF", "JPY", "CNY", "INR", "RUB", "KRW", "ILS", "TRY",
            "PLN", "CZK", "SEK", "NOK", "DKK", "BRL", "CAD", "AUD", "NZD", "SGD", "HKD",
            "MXN", "ZAR", "HUF", "RON", "BGN", "HRK", "AED", "SAR", "THB", "MYR", "IDR",
            "PHP", "VND", "ARS", "CLP", "COP", "PEN", "UYU", "EGP", "MAD", "TND", "PKR",
            "LKR", "NPR", "ISK", "UAH", "RSD", "TWD"
        };

        private static readonly Regex IsoCodeInText = new(@"\b[A-Z]{3}\b", RegexOptions.Compiled);

        /// <summary>
        /// Legge codice e simbolo dal testo completo del documento.
        /// <para>
        /// Il codice si accetta solo se ne compare <b>uno solo</b>: due codici diversi nello stesso
        /// documento vogliono dire che uno dei due non e' quello della spesa (una nota di cambio,
        /// le condizioni di pagamento), e da fuori non c'e' modo di sapere quale.
        /// </para>
        /// </summary>
        public static (string Code, string Symbol) Read(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return (null, null);

            var codes = IsoCodeInText.Matches(content.ToUpperInvariant())
                .Select(match => match.Value)
                .Where(KnownCodes.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return (codes.Count == 1 ? codes[0] : null, ExtractSymbol(content));
        }

        /// <summary>
        /// Primo codice ISO noto che compare nel testo. Si usa sul contenuto di un singolo campo
        /// ("12,20 EUR"), dove non c'e' spazio per l'ambiguita' del documento intero.
        /// </summary>
        public static string ExtractIsoCode(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            foreach (Match match in IsoCodeInText.Matches(content.ToUpperInvariant()))
            {
                if (KnownCodes.Contains(match.Value))
                    return match.Value;
            }

            return null;
        }

        public static string ExtractSymbol(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            foreach (var symbol in UnambiguousSymbols.Keys)
            {
                if (content.Contains(symbol, StringComparison.Ordinal))
                    return symbol;
            }

            foreach (var symbol in AmbiguousSymbols.Keys)
            {
                if (content.Contains(symbol, StringComparison.Ordinal))
                    return symbol;
            }

            return null;
        }

        /// <summary>Valuta suggerita dal paese dell'esercente, o null se l'indirizzo non dice niente.</summary>
        public static string GuessFromAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            var normalized = address.ToUpperInvariant();

            foreach (var (pattern, currency) in AddressHints)
            {
                if (Regex.IsMatch(normalized, pattern))
                    return currency;
            }

            return null;
        }
    }
}
