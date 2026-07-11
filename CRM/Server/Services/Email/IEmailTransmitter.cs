using MimeKit;

namespace CRM.Server.Services.Email
{
    /// <summary>
    /// Canale di trasmissione di un messaggio email già renderizzato. Le implementazioni
    /// (SMTP, in futuro un ESP come Brevo) sono intercambiabili: l'outbox le prova in
    /// catena (primario → fallback) così l'indisponibilità di un canale non blocca l'invio.
    /// Deve <b>lanciare eccezione</b> se la trasmissione non riesce, così il worker passa
    /// al trasmettitore successivo.
    /// </summary>
    public interface IEmailTransmitter
    {
        /// <summary>Nome leggibile del canale (per diagnostica/log, es. "SMTP primario").</summary>
        string Name { get; }

        /// <summary>
        /// Trasmette il messaggio e restituisce la risposta del server (per il log).
        /// <paramref name="messageRef"/> è l'identificatore di correlazione da agganciare all'invio
        /// (custom arg/tag) così che gli eventi di engagement del provider siano ricondotti all'email;
        /// i canali SMTP puri, privi di tracking, lo ignorano.
        /// </summary>
        Task<string> SendAsync(MimeMessage message, string? messageRef = null, CancellationToken ct = default);
    }
}
