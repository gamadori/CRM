using System;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared
{
    /// <summary>Stato di consegna di una email accodata (Pending → Sent | Failed).</summary>
    public enum EmailOutboxStatus
    {
        /// <summary>In coda, non ancora tentata.</summary>
        Pending = 0,
        /// <summary>Inviata con successo.</summary>
        Sent = 1,
        /// <summary>Ultimo tentativo fallito; verrà ritentato finché non si esauriscono i tentativi.</summary>
        Failed = 2
    }

    /// <summary>
    /// Coda di invio email (outbox pattern). La richiesta accoda qui il messaggio già renderizzato
    /// (MIME serializzato in <see cref="Payload"/>, lossless: template/logo inline/allegati inclusi)
    /// e ritorna subito; un background service lo trasmette via SMTP con retry e backoff.
    /// Le colonne envelope (To/Cc/Subject/...) sono denormalizzate per log, diagnostica e viste admin.
    /// </summary>
    public class EmailOutbox
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Tipo funzionale dell'email (per diagnostica/filtri).</summary>
        public EmailsTypes EmailType { get; set; }

        /// <summary>Destinatari, separati da ';' (denormalizzato per log/vista).</summary>
        public string To { get; set; } = string.Empty;

        public string? Cc { get; set; }

        public string Subject { get; set; } = string.Empty;

        /// <summary>Percorsi degli allegati (denormalizzato per il log EmailSent).</summary>
        public string? Attachments { get; set; }

        /// <summary>Messaggio MIME completo serializzato (RFC 822): trasmesso verbatim dal worker.</summary>
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        /// <summary>Utente che ha accodato l'email (propagato a EmailSent e all'attività di timeline).</summary>
        public string? IdUser { get; set; }

        // ---- Aggancio CRM opzionale (Tier 1): registra la comunicazione nella timeline dell'entità ----

        public ActivityEntityType? EntityType { get; set; }

        public int? EntityId { get; set; }

        // ---- Macchina a stati di consegna (stesso pattern dei promemoria) ----

        public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pending;

        /// <summary>Numero di tentativi di invio già effettuati.</summary>
        public int RetryCount { get; set; }

        /// <summary>Istante dell'ultimo tentativo (per il backoff tra i retry).</summary>
        public DateTime? LastAttemptAt { get; set; }

        /// <summary>Messaggio dell'ultimo errore di invio (diagnostica).</summary>
        public string? LastError { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Istante di invio riuscito.</summary>
        public DateTime? SentAt { get; set; }
    }

    public class EmailOutboxFilterModel : PagingParameterModel
    {
        public EmailOutboxStatus? Status { get; set; }
    }
}
