using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>Tipo di evento di engagement normalizzato (comune ai vari provider ESP).</summary>
    public enum EmailEventType
    {
        Delivered,
        Opened,
        Clicked,
        Bounced,
        SpamReported,
        Unsubscribed,
        Deferred,
        Dropped,
        Other
    }

    /// <summary>
    /// Singolo evento di engagement ricevuto da un provider via webhook (storico completo).
    /// Collegato all'email inviata tramite <see cref="IdEmailSent"/> (risolto per MessageRef).
    /// </summary>
    public class EmailEvent
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(EmailSent))]
        public int IdEmailSent { get; set; }

        public EmailProvider Provider { get; set; }

        public EmailEventType Type { get; set; }

        /// <summary>Istante dell'evento dichiarato dal provider.</summary>
        public DateTime OccurredAt { get; set; }

        /// <summary>URL cliccato (per gli eventi Clicked).</summary>
        public string? Url { get; set; }

        /// <summary>Dettaglio aggiuntivo (motivo del bounce, ecc.).</summary>
        public string? Detail { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual EmailSent? EmailSent { get; set; }
    }
}
