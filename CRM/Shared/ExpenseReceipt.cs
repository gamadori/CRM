using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>
    /// Rappresenta una nota spese associata a un intervento ticket.
    /// Può essere testuale o avere un documento PDF allegato.
    /// </summary>
    public class ExpenseReceipt
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID dell'intervento a cui è associata questa nota spese
        /// </summary>
        [ForeignKey("TicketIntervention")]
        [Required]
        public int TicketInterventionId { get; set; }

        /// <summary>
        /// Navigazione verso l'intervento
        /// </summary>
        public TicketIntervention? TicketIntervention { get; set; }

        /// <summary>
        /// Descrizione testuale della nota spese
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// ID del file attachment contenente lo scontrino/fattura PDF (opzionale)
        /// </summary>
        [ForeignKey("AttachmentFile")]
        public int? AttachmentFileId { get; set; }

        /// <summary>
        /// Navigazione verso il file allegato
        /// </summary>
        public AttachmentFile? AttachmentFile { get; set; }

        /// <summary>
        /// Importo totale estratto automaticamente o inserito manualmente
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalAmount { get; set; }

        /// <summary>
        /// Importo IVA/Tasse estratto o inserito manualmente
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TaxAmount { get; set; }

        /// <summary>
        /// Data transazione estratta dallo scontrino o inserita manualmente
        /// </summary>
        public DateTime? TransactionDate { get; set; }

        /// <summary>
        /// Nome commerciante estratto o inserito manualmente
        /// </summary>
        [MaxLength(200)]
        public string? MerchantName { get; set; }

        /// <summary>
        /// Valuta (EUR, USD, ecc.)
        /// </summary>
        [MaxLength(10)]
        public string? Currency { get; set; }

        /// <summary>
        /// Confidence media dell'estrazione automatica (0-1). Null se inserito manualmente
        /// </summary>
        public float? ExtractionConfidence { get; set; }

        /// <summary>
        /// Data e ora dell'elaborazione automatica. Null se inserito manualmente
        /// </summary>
        public DateTime? ProcessedDate { get; set; }

        /// <summary>
        /// Se true, i dati estratti sono stati confermati dall'utente
        /// </summary>
        public bool IsConfirmed { get; set; } = false;

        /// <summary>
        /// Data conferma da parte dell'utente
        /// </summary>
        public DateTime? ConfirmedDate { get; set; }

        /// <summary>
        /// ID utente che ha confermato
        /// </summary>
        public string? ConfirmedByUserId { get; set; }

        /// <summary>
        /// JSON raw dei campi estratti (per debug/audit)
        /// </summary>
        public string? ExtractedFieldsJson { get; set; }

        /// <summary>
        /// Data creazione record
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Data ultima modifica
        /// </summary>
        public DateTime? LastModifiedDate { get; set; }

        /// <summary>
        /// Note aggiuntive
        /// </summary>
        [MaxLength(1000)]
        public string? Notes { get; set; }
    }
}
