using System.Globalization;

namespace CRM.Client.Helpers
{
    /// <summary>
    /// Formattazione degli importi con la valuta della spesa.
    /// <para>
    /// Sta in un punto solo perche' il simbolo scritto a mano nella pagina e' stato il difetto
    /// vero: mostrava l'euro su uno scontrino in dollari, cioe' un importo credibile e sbagliato.
    /// Il simbolo si ricava dal codice della riga, e quando il codice non e' fra quelli noti si
    /// mostra il codice stesso: "CHF 20,00" e' corretto, "€ 20,00" no.
    /// </para>
    /// </summary>
    public static class CurrencyUi
    {
        public static string Symbol(string currency) => currency switch
        {
            "EUR" => "€",
            "USD" => "$",
            "GBP" => "£",
            "JPY" => "¥",
            "CHF" => "CHF",
            null or "" => "",
            _ => currency
        };

        /// <summary>Importo formattato in cifre italiane, preceduto dal simbolo. "-" se assente.</summary>
        public static string Money(decimal? amount, string currency)
        {
            if (!amount.HasValue) return "-";

            var value = amount.Value.ToString("N2", CultureInfo.GetCultureInfo("it-IT"));
            var symbol = Symbol(currency);

            return string.IsNullOrEmpty(symbol) ? value : $"{symbol} {value}";
        }
    }
}
