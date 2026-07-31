using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared.DTOs
{
    /// <summary>
    /// DTO per la visualizzazione di una nota spese
    /// </summary>
    public class ExpenseReceiptDTO
    {
        public int Id { get; set; }

        /// <summary>Intervento di riferimento; null sulle spese che non nascono da un intervento.</summary>
        public int? TicketInterventionId { get; set; }

        /// <summary>Attivita' di riferimento (visita commerciale o di cortesia); null altrimenti.</summary>
        public int? IdActivity { get; set; }

        /// <summary>Oggetto dell'attivita', per mostrare il contesto senza una seconda chiamata.</summary>
        public string? ActivitySubject { get; set; }

        public string? IdUserSpender { get; set; }

        public string? UserSpenderName { get; set; }

        /// <summary>Cambio congelato verso la valuta base; 1 se la spesa e' gia' in valuta base.</summary>
        public decimal? ExchangeRate { get; set; }

        /// <summary>Importo in valuta base. Null = ancora da convertire, e come tale va mostrata.</summary>
        public decimal? AmountBase { get; set; }

        /// <summary>
        /// True quando c'e' un importo ma manca la conversione: la riga non entra nei totali e
        /// deve essere visibile come da sistemare, non sparire.
        /// </summary>
        public bool NeedsConversion => TotalAmount.HasValue && !AmountBase.HasValue;

        public string? Description { get; set; }

        public int? AttachmentFileId { get; set; }

        public string? AttachmentFileName { get; set; }

        public decimal? TotalAmount { get; set; }

        public decimal? TaxAmount { get; set; }

        public DateTime? TransactionDate { get; set; }

        public string? MerchantName { get; set; }

        public string? Currency { get; set; }

        public float? ExtractionConfidence { get; set; }

        /// <summary>
        /// Risultato completo dell'estrazione, inclusi i documenti fiscali distinti contenuti
        /// nello stesso allegato.
        /// </summary>
        public string? ExtractedFieldsJson { get; set; }

        public DateTime? ProcessedDate { get; set; }

        public bool IsConfirmed { get; set; }

        public DateTime? ConfirmedDate { get; set; }

        public string? ConfirmedByUserId { get; set; }

        public string? ConfirmedByUserName { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? LastModifiedDate { get; set; }

        public string? Notes { get; set; }

        public List<ExpenseReceiptDocumentDTO> Documents { get; set; } = new();

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

        /// <summary>
        /// Contesto facoltativo. Al massimo uno fra intervento e attivita'; entrambi nulli
        /// significa costo generale, che e' un caso legittimo.
        /// </summary>
        public int? TicketInterventionId { get; set; }

        public int? IdActivity { get; set; }

        /// <summary>Chi ha speso. Se non valorizzato, il server usa l'utente corrente.</summary>
        public string? IdUserSpender { get; set; }

        /// <summary>Cambio verso la valuta base. Se null il server lo propone dal servizio cambi.</summary>
        public decimal? ExchangeRate { get; set; }

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

        public List<ExpenseReceiptDocumentDTO> Documents { get; set; } = new();
    }

    public class ExpenseReceiptDocumentDTO
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public int? PageFrom { get; set; }
        public int? PageTo { get; set; }
        public string? DocumentType { get; set; }
        public string? MerchantName { get; set; }
        public DateTime? TransactionDate { get; set; }
        public string? Currency { get; set; }
        public decimal? SubtotalAmount { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? TotalAmount { get; set; }
        public decimal? ExchangeRate { get; set; }
        public decimal? AmountBase { get; set; }
        public decimal? TaxAmountBase { get; set; }
        public float? ExtractionConfidence { get; set; }
        public List<ExpenseReceiptDocumentLineDTO> Lines { get; set; } = new();
    }

    public class ExpenseReceiptDocumentLineDTO
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? TotalPrice { get; set; }
        public float? ExtractionConfidence { get; set; }
    }

    /// <summary>
    /// DTO per il riepilogo delle note spese di un intervento
    /// </summary>
    public class ExpenseReceiptSummaryDTO
    {
        /// <summary>Valorizzato solo quando il riepilogo e' quello di un singolo intervento.</summary>
        public int? TicketInterventionId { get; set; }

        public int TotalReceiptsCount { get; set; }

        public int ConfirmedReceiptsCount { get; set; }

        public int PendingReceiptsCount { get; set; }

        public decimal TotalExpenses { get; set; }

        public decimal TotalTaxes { get; set; }

        public decimal TotalNet => TotalExpenses - TotalTaxes;

        /// <summary>Valuta in cui sono espressi i totali.</summary>
        public string BaseCurrency { get; set; } = "EUR";

        /// <summary>
        /// Spese con un importo ma senza conversione: NON sono nei totali qui sopra.
        /// Vanno mostrate, perche' un totale che nasconde tre scontrini esteri e' peggio di un
        /// totale mancante.
        /// </summary>
        public int NeedsConversionCount { get; set; }

        /// <summary>Spaccato per valuta originale, per capire da dove arrivano i totali.</summary>
        public List<ExpenseCurrencyBreakdownDTO> ByCurrency { get; set; } = new();

        /// <summary>Spaccato per persona, la domanda vera del rimborso.</summary>
        public List<ExpenseUserBreakdownDTO> ByUser { get; set; } = new();
    }

    /// <summary>Contesto della spesa, usato come filtro nella pagina globale.</summary>
    public enum ExpenseContextFilter
    {
        All = 0,
        /// <summary>Legate a un intervento.</summary>
        Intervention = 1,
        /// <summary>Legate a un'attivita' (visita commerciale o di cortesia).</summary>
        Activity = 2,
        /// <summary>Senza contesto: costi generali.</summary>
        None = 3
    }

    public class ExpenseReceiptFilter : PagingParameterModel
    {
        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        /// <summary>Chi ha speso. Null = tutti (se chi chiede ne ha il diritto).</summary>
        public string? IdUserSpender { get; set; }

        public ExpenseContextFilter Context { get; set; } = ExpenseContextFilter.All;

        public bool? IsConfirmed { get; set; }

        /// <summary>Solo le spese che hanno un importo ma non una conversione.</summary>
        public bool? NeedsConversion { get; set; }

        public string? Search { get; set; }
    }

    public class ExpenseCurrencyBreakdownDTO
    {
        public string? Currency { get; set; }

        public int Count { get; set; }

        /// <summary>Totale nella valuta originale.</summary>
        public decimal TotalOriginal { get; set; }

        /// <summary>Totale convertito in valuta base; esclude le righe senza cambio.</summary>
        public decimal TotalBase { get; set; }
    }

    public class ExpenseUserBreakdownDTO
    {
        public string? IdUser { get; set; }

        public string? UserName { get; set; }

        public int Count { get; set; }

        public decimal TotalBase { get; set; }
    }
}
