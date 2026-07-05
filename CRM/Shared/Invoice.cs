using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CRM.Shared
{
    public enum InvoiceStates
    {
        Draft,      // bozza, non ancora numerata/emessa
        Issued,     // emessa (numero fiscale assegnato), XML generabile
        Sent,       // trasmessa a SdI tramite provider
        Delivered,  // consegnata (notifica RC)
        Rejected    // scartata da SdI (notifica NS)
    }

    /// <summary>
    /// Fattura fiscale (testata). Parte indipendente dal provider: contiene i dati per
    /// generare l'XML FatturaPA. La trasmissione a SdI e' delegata a un IEInvoiceProvider.
    /// </summary>
    public class Invoice
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Numero")]
        public string? Number { get; set; }

        [Display(Name = "Data")]
        public DateTime Date { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Azienda")]
        [ForeignKey(nameof(Company))]
        public int? IdCompany { get; set; }

        [Display(Name = "Contatto")]
        [ForeignKey(nameof(Contact))]
        public int? IdContact { get; set; }

        [Display(Name = "Ordine")]
        [ForeignKey(nameof(Order))]
        public int? IdOrder { get; set; }

        [Display(Name = "Owner")]
        [ForeignKey(nameof(User))]
        public string? IdUser { get; set; }

        /// <summary>Tipo documento FatturaPA (TD01 = fattura).</summary>
        [Display(Name = "Tipo documento")]
        public string TipoDocumento { get; set; } = "TD01";

        /// <summary>Codice destinatario SdI (7 caratteri), oppure "0000000" con PEC valorizzata.</summary>
        [Display(Name = "Codice destinatario")]
        public string CodiceDestinatario { get; set; } = "0000000";

        [Display(Name = "PEC destinatario")]
        public string? PecDestinatario { get; set; }

        [Display(Name = "Causale")]
        public string? Causale { get; set; }

        [Display(Name = "Note")]
        public string? Note { get; set; }

        [Display(Name = "Stato")]
        public InvoiceStates State { get; set; } = InvoiceStates.Draft;

        [Column(TypeName = "Money")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "Money")]
        public decimal TotalVat { get; set; }

        [Column(TypeName = "Money")]
        public decimal Total { get; set; }

        /// <summary>Identificativo restituito dal provider SdI (SdI Id / riferimento invio).</summary>
        [Display(Name = "Rif. SdI")]
        public string? SdiReference { get; set; }

        public virtual Company? Company { get; set; }

        public virtual Contact? Contact { get; set; }

        public virtual Order? Order { get; set; }

        public virtual ApplicationUser? User { get; set; }

        public virtual ICollection<InvoiceRow> Rows { get; set; } = new List<InvoiceRow>();
    }

    public class InvoiceRow
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Invoice))]
        public int IdInvoice { get; set; }

        [ForeignKey(nameof(Product))]
        public int? IdProduct { get; set; }

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

        /// <summary>Natura IVA per righe con aliquota 0 (es. N1, N2.2, N3.5...). Obbligatoria a norma se VatRate = 0.</summary>
        [Display(Name = "Natura")]
        public string? Natura { get; set; }

        public int SortOrder { get; set; }

        [Column(TypeName = "Money")]
        public decimal LineNet { get; set; }

        [Column(TypeName = "Money")]
        public decimal LineVat { get; set; }

        [Column(TypeName = "Money")]
        public decimal LineTotal { get; set; }

        [JsonIgnore]
        public virtual Invoice? Invoice { get; set; }

        public virtual Product? Product { get; set; }
    }

    public class InvoiceFilter : PagingParameterModel
    {
        public string? Search { get; set; }

        public int? IdCompany { get; set; }

        public int? IdOrder { get; set; }

        public string? IdUser { get; set; }

        public InvoiceStates? State { get; set; }
    }
}
