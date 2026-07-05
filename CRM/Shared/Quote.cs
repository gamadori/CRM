using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CRM.Shared
{
    public enum QuoteStates
    {
        Draft,
        Sent,
        Accepted,
        Rejected,
        Expired
    }

    /// <summary>
    /// Testata del preventivo/offerta. Puo' essere agganciata a un Deal o vivere autonoma su un'azienda.
    /// I totali sono ricalcolati e persistiti lato server (mai fidarsi del client).
    /// </summary>
    public class Quote
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Numero")]
        public string? Number { get; set; }

        [Display(Name = "Data")]
        public DateTime Date { get; set; }

        [Display(Name = "Valida fino al")]
        public DateTime? ValidUntil { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Azienda")]
        [ForeignKey(nameof(Company))]
        public int? IdCompany { get; set; }

        [Display(Name = "Contatto")]
        [ForeignKey(nameof(Contact))]
        public int? IdContact { get; set; }

        [Display(Name = "Trattativa")]
        [ForeignKey(nameof(Deal))]
        public int? IdDeal { get; set; }

        [Display(Name = "Owner")]
        [ForeignKey(nameof(User))]
        public string? IdUser { get; set; }

        [Display(Name = "Stato")]
        public QuoteStates State { get; set; } = QuoteStates.Draft;

        [Display(Name = "Note")]
        public string? Note { get; set; }

        [Display(Name = "Termini e condizioni")]
        public string? TermsConditions { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = "Imponibile")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = "Sconto totale")]
        public decimal TotalDiscount { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = "IVA")]
        public decimal TotalVat { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = "Totale")]
        public decimal Total { get; set; }

        public virtual Company? Company { get; set; }

        public virtual Contact? Contact { get; set; }

        public virtual Deal? Deal { get; set; }

        public virtual ApplicationUser? User { get; set; }

        public virtual ICollection<QuoteRow> Rows { get; set; } = new List<QuoteRow>();
    }

    /// <summary>
    /// Riga del preventivo. Descrizione e prezzo sono uno snapshot congelato all'inserimento:
    /// se domani cambia il catalogo, le offerte gia' emesse non cambiano.
    /// </summary>
    public class QuoteRow
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Quote))]
        public int IdQuote { get; set; }

        [Display(Name = "Prodotto")]
        [ForeignKey(nameof(Product))]
        public int? IdProduct { get; set; }

        [Display(Name = "Matricola")]
        [ForeignKey(nameof(Article))]
        public int? IdArticle { get; set; }

        [Display(Name = "Descrizione")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Quantita'")]
        public decimal Quantity { get; set; } = 1;

        [Column(TypeName = "Money")]
        [Display(Name = "Prezzo unitario")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Sconto %")]
        public decimal DiscountPct { get; set; }

        [Display(Name = "IVA %")]
        public decimal VatRate { get; set; }

        public int SortOrder { get; set; }

        [Column(TypeName = "Money")]
        public decimal LineNet { get; set; }

        [Column(TypeName = "Money")]
        public decimal LineVat { get; set; }

        [Column(TypeName = "Money")]
        public decimal LineTotal { get; set; }

        [JsonIgnore]
        public virtual Quote? Quote { get; set; }

        public virtual Product? Product { get; set; }

        public virtual Article? Article { get; set; }
    }

    public class QuoteFilter : PagingParameterModel
    {
        public string? Search { get; set; }

        public int? IdCompany { get; set; }

        public int? IdDeal { get; set; }

        public string? IdUser { get; set; }

        public QuoteStates? State { get; set; }
    }
}
