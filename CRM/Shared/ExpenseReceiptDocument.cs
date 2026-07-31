using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    public class ExpenseReceiptDocument
    {
        [Key]
        public int Id { get; set; }

        public int ExpenseReceiptId { get; set; }
        public ExpenseReceipt ExpenseReceipt { get; set; } = null!;

        public int SortOrder { get; set; }

        public int? PageFrom { get; set; }
        public int? PageTo { get; set; }

        [MaxLength(100)]
        public string? DocumentType { get; set; }

        [MaxLength(200)]
        public string? MerchantName { get; set; }

        public DateTime? TransactionDate { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SubtotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? ExchangeRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? AmountBase { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TaxAmountBase { get; set; }

        public float? ExtractionConfidence { get; set; }

        public ICollection<ExpenseReceiptDocumentLine> Lines { get; set; } = new List<ExpenseReceiptDocumentLine>();
    }

    public class ExpenseReceiptDocumentLine
    {
        [Key]
        public int Id { get; set; }

        public int ExpenseReceiptDocumentId { get; set; }
        public ExpenseReceiptDocument ExpenseReceiptDocument { get; set; } = null!;

        public int SortOrder { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal? Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalPrice { get; set; }

        public float? ExtractionConfidence { get; set; }
    }
}
