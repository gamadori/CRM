using System.ComponentModel.DataAnnotations;

namespace CRM.Shared.DTOs
{
    public class PriceListItemDTO
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Azienda")]
        public int IdCompany { get; set; }

        [Required]
        [Display(Name = "Prodotto")]
        public int IdProduct { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string? ProductCode { get; set; }

        [Display(Name = "Prezzo di catalogo")]
        public decimal CatalogPrice { get; set; }

        [Display(Name = "Prezzo")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Sconto %")]
        public decimal DiscountPct { get; set; }
    }

    public static class PriceListHelper
    {
        public static PriceListItemDTO? ToDTO(this PriceListItem item)
        {
            if (item == null) return null;

            return new PriceListItemDTO
            {
                Id = item.Id,
                IdCompany = item.IdCompany,
                IdProduct = item.IdProduct,
                ProductName = item.Product != null ? item.Product.Name : string.Empty,
                ProductCode = item.Product?.Code,
                CatalogPrice = item.Product != null ? item.Product.Price : 0m,
                UnitPrice = item.UnitPrice,
                DiscountPct = item.DiscountPct
            };
        }

        public static PriceListItem ToEntity(this PriceListItemDTO dto)
        {
            if (dto == null) return null;

            return new PriceListItem
            {
                Id = dto.Id,
                IdCompany = dto.IdCompany,
                IdProduct = dto.IdProduct,
                UnitPrice = dto.UnitPrice,
                DiscountPct = dto.DiscountPct
            };
        }
    }
}
