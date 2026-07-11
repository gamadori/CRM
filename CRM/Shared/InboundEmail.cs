using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>
    /// Email ricevuta e processata (registro + deduplica + esito routing). Serve sia da audit sia da
    /// area di "non associate" quando il mittente non è riconducibile a una scheda.
    /// </summary>
    public class InboundEmail
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Inbox))]
        public int IdInbox { get; set; }

        /// <summary>Message-Id RFC del messaggio (deduplica).</summary>
        public string? MessageId { get; set; }

        /// <summary>UID IMAP nel folder di origine (deduplica quando manca il Message-Id).</summary>
        public long? Uid { get; set; }

        public string? FromAddress { get; set; }

        public string? FromName { get; set; }

        public string? ToAddress { get; set; }

        public string? Subject { get; set; }

        /// <summary>Corpo ripulito dalla cronologia citata.</summary>
        public string? Body { get; set; }

        public DateTime ReceivedAt { get; set; }

        // ---- Esito routing ----

        public int? IdContact { get; set; }

        public int? IdCompany { get; set; }

        /// <summary>Attività creata sulla timeline (se il mittente è stato associato).</summary>
        public int? IdActivity { get; set; }

        /// <summary>Ticket creato o a cui il messaggio è stato agganciato (threading).</summary>
        public int? IdTicket { get; set; }

        /// <summary>True se il mittente è stato associato a una scheda (azienda).</summary>
        public bool IsMatched { get; set; }

        /// <summary>True quando un operatore ha preso in carico l'email (aperta/associata/ticket creato): esce dagli avvisi.</summary>
        public bool Handled { get; set; }

        // ---- Esito del triage AI (null se l'AI non è stata usata o non era disponibile) ----

        /// <summary>Verdetto AI: l'email è una richiesta di assistenza che merita un ticket.</summary>
        public bool? AiIsSupportRequest { get; set; }

        /// <summary>Confidenza del verdetto AI (0..1).</summary>
        public double? AiConfidence { get; set; }

        /// <summary>Riassunto operativo prodotto dall'AI (usato come descrizione del ticket).</summary>
        public string? AiSummary { get; set; }

        /// <summary>Motivazione del verdetto AI, mostrata all'operatore.</summary>
        public string? AiReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual EmailInbox? Inbox { get; set; }
    }
}
