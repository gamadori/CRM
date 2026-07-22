using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CRM.Shared
{
    public enum OrderStates
    {
        Confirmed,
        InProduction,
        Delivered,
        Invoiced,
        Cancelled
    }

    /// <summary>
    /// Ordine, tipicamente generato dall'accettazione di un preventivo (Quote).
    /// Totali ricalcolati e persistiti lato server.
    /// </summary>
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Numero")]
        public string? Number { get; set; }

        [Display(Name = "Data")]
        public DateTime Date { get; set; }

        [Display(Name = "Consegna prevista")]
        public DateTime? DeliveryDate { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Azienda")]
        [ForeignKey(nameof(Company))]
        public int? IdCompany { get; set; }

        [Display(Name = "Contatto")]
        [ForeignKey(nameof(Contact))]
        public int? IdContact { get; set; }

        [Display(Name = "Preventivo")]
        [ForeignKey(nameof(Quote))]
        public int? IdQuote { get; set; }

        [Display(Name = "Trattativa")]
        [ForeignKey(nameof(Deal))]
        public int? IdDeal { get; set; }

        [Display(Name = "Owner")]
        [ForeignKey(nameof(User))]
        public string? IdUser { get; set; }

        [Display(Name = "Stato")]
        public OrderStates State { get; set; } = OrderStates.Confirmed;

        [Display(Name = "Note")]
        public string? Note { get; set; }

        [Column(TypeName = "Money")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "Money")]
        public decimal TotalDiscount { get; set; }

        [Column(TypeName = "Money")]
        public decimal TotalVat { get; set; }

        [Column(TypeName = "Money")]
        public decimal Total { get; set; }

        public virtual Company? Company { get; set; }

        public virtual Contact? Contact { get; set; }

        public virtual Quote? Quote { get; set; }

        public virtual Deal? Deal { get; set; }

        public virtual ApplicationUser? User { get; set; }

        public virtual ICollection<OrderRow> Rows { get; set; } = new List<OrderRow>();

        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }

    public class OrderRow
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Order))]
        public int IdOrder { get; set; }

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
        public virtual Order? Order { get; set; }

        public virtual Product? Product { get; set; }

        public virtual Article? Article { get; set; }
    }

    public class OrderFilter : PagingParameterModel
    {
        public string? Search { get; set; }

        public int? IdCompany { get; set; }

        public int? IdQuote { get; set; }

        public int? IdDeal { get; set; }

        public string? IdUser { get; set; }

        public OrderStates? State { get; set; }
    }
}
