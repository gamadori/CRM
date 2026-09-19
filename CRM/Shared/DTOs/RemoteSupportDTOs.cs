using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    /// <summary>Codice monouso da inserire sul pannello (Setup → Assistenza remota).</summary>
    public class RemoteSupportCodeDTO
    {
        public string Code { get; set; } = string.Empty;

        /// <summary>Scadenza del codice, Unix time in secondi.</summary>
        public long ExpiresAt { get; set; }
    }

    /// <summary>
    /// Stato del pannello sul server di assistenza. <see cref="State"/>:
    /// <c>NotConfigured</c> (mai registrato dal CRM), <c>Pending</c> (codice emesso,
    /// non ancora abbinato), <c>Expired</c> (codice scaduto), <c>Ready</c> (schermo
    /// raggiungibile), <c>Offline</c> (abbinato ma non risponde), <c>Revoked</c>.
    /// </summary>
    public class RemoteSupportStatusDTO
    {
        public string State { get; set; } = "NotConfigured";
        public string? Address { get; set; }
    }

    /// <summary>Apertura di una sessione di assistenza su una o più macchine.</summary>
    /// <remarks>
    /// Più macchine in <see cref="ArticleIds"/> finiscono in un <b>unico</b> accesso
    /// Guacamole con più connessioni: così il tecnico le vede insieme in una sola
    /// finestra, senza il conflitto di sessione del browser (una sola sessione
    /// Guacamole per browser) che lascerebbe la seconda connessione vuota.
    /// </remarks>
    public class RemoteSupportStartRequest
    {
        public List<int> ArticleIds { get; set; } = new();
    }

    /// <summary>Esito dell'apertura: l'URL Guacamole e quali macchine sono incluse.</summary>
    public class RemoteSupportStartResultDTO
    {
        public string Url { get; set; } = string.Empty;

        /// <summary>Macchine effettivamente raggiungibili e incluse nell'accesso.</summary>
        public List<int> Included { get; set; } = new();

        /// <summary>Macchine saltate perché non pronte (non registrate, offline, revocate).</summary>
        public List<int> Skipped { get; set; } = new();
    }
}
