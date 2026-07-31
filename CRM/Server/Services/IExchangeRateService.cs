using System;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    /// <summary>
    /// Cambi di riferimento per convertire le spese sostenute all'estero.
    /// </summary>
    public interface IExchangeRateService
    {
        /// <summary>
        /// Quante unita' di <paramref name="toCurrency"/> vale 1 unita' di
        /// <paramref name="fromCurrency"/> alla data indicata.
        /// <para>
        /// Restituisce null quando il cambio non e' determinabile (servizio irraggiungibile,
        /// valuta non coperta, codice non valido). Il chiamante NON deve inventare un valore:
        /// la spesa resta da convertire e la pagina la mostra come tale.
        /// </para>
        /// </summary>
        Task<decimal?> GetRateAsync(string fromCurrency, string toCurrency, DateTime date);
    }
}
