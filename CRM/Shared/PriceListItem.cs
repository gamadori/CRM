using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CRM.Shared
{
    /// <summary>
    /// Voce di listino: prezzo (e sconto) di un prodotto per uno specifico cliente.
    /// In fase di preventivo/ordine, se esiste una voce per (azienda, prodotto) la si usa
    /// al posto del prezzo di catalogo.
    /// </summary>
    public class PriceListItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Azienda")]
        [ForeignKey(nameof(Company))]
        public int IdCompany { get; set; }

        [Required]
        [Display(Name = "Prodotto")]
        [ForeignKey(nameof(Product))]
        public int IdProduct { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = "Prezzo")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Sconto %")]
        public decimal DiscountPct { get; set; }

        [JsonIgnore]
        public virtual Company? Company { get; set; }

        public virtual Product? Product { get; set; }
    }

    public class PriceListFilter : PagingParameterModel
    {
        public int? IdCompany { get; set; }

        public int? IdProduct { get; set; }

        public string? Search { get; set; }
    }
}
