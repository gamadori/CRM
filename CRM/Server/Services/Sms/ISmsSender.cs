using System.Threading;
using System.Threading.Tasks;

namespace CRM.Server.Services.Sms
{
    /// <summary>
    /// Astrazione per l'invio di SMS. Implementazioni: <c>TwilioSmsSender</c> (REST)
    /// e <c>NullSmsSender</c> (nessun provider configurato). Il resto del codice non
    /// dipende dal fornitore concreto.
    /// </summary>
    public interface ISmsSender
    {
        /// <summary>True se un provider SMS è realmente configurato e utilizzabile.</summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Invia un SMS. Il numero deve essere in formato E.164 (es. "+391234567890").
        /// Ritorna true se il messaggio è stato accettato dal provider.
        /// </summary>
        Task<bool> SendAsync(string toPhoneE164, string text, CancellationToken ct = default);
    }
}
