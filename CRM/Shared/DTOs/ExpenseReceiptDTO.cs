using System;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared.DTOs
{
    /// <summary>
    /// DTO per la visualizzazione di una nota spese
    /// </summary>
    public class ExpenseReceiptDTO
    {
        public int Id { get; set; }

        public int TicketInterventionId { get; set; }

        public string? Description { get; set; }

        public int? AttachmentFileId { get; set; }

        public string? AttachmentFileName { get; set; }

        public decimal? TotalAmount { get; set; }

        public decimal? TaxAmount { get; set; }

        public DateTime? TransactionDate { get; set; }

        public string? MerchantName { get; set; }

        public string? Currency { get; set; }

        public float? ExtractionConfidence { get; set; }

        public DateTime? ProcessedDate { get; set; }

        public bool IsConfirmed { get; set; }

        public DateTime? ConfirmedDate { get; set; }

        public string? ConfirmedByUserId { get; set; }

        public string? ConfirmedByUserName { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? LastModifiedDate { get; set; }

        public string? Notes { get; set; }

        /// <summary>
        /// Importo netto (Totale - Tasse)
        /// </summary>
        public decimal? NetAmount => TotalAmount.HasValue && TaxAmount.HasValue 
            ? TotalAmount.Value - TaxAmount.Value 
            : null;

        /// <summary>
        /// Indica se ha un documento PDF allegato
        /// </summary>
        public bool HasAttachment => AttachmentFileId.HasValue;

        /// <summary>
        /// Indica se è stata estratta automaticamente
        /// </summary>
        public bool WasAutoExtracted => ExtractionConfidence.HasValue;
    }

    /// <summary>
    /// DTO per la creazione/aggiornamento di una nota spese
    /// </summary>
    public class ExpenseReceiptCreateUpdateDTO
    {
        public int? Id { get; set; }

        [Required]
        public int TicketInterventionId { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public int? AttachmentFileId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "L'importo totale deve essere positivo")]
        public decimal? TotalAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "L'importo delle tasse deve essere positivo")]
        public decimal? TaxAmount { get; set; }

        public DateTime? TransactionDate { get; set; }

        [MaxLength(200)]
        public string? MerchantName { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public bool IsConfirmed { get; set; }

        // Campi estratti automaticamente (non modificabili dall'utente direttamente)
        public float? ExtractionConfidence { get; set; }
        public string? ExtractedFieldsJson { get; set; }
    }

    /// <summary>
    /// DTO per il riepilogo delle note spese di un intervento
    /// </summary>
    public class ExpenseReceiptSummaryDTO
    {
        public int TicketInterventionId { get; set; }

        public int TotalReceiptsCount { get; set; }

        public int ConfirmedReceiptsCount { get; set; }

        public int PendingReceiptsCount { get; set; }

        public decimal TotalExpenses { get; set; }

        public decimal TotalTaxes { get; set; }

        public decimal TotalNet => TotalExpenses - TotalTaxes;
    }
}
