using System;

namespace CRM.Shared.DTOs
{
    public class ProductInterestDTO
    {
        public int Id { get; set; }

        public int IdProduct { get; set; }

        public string ProductCode { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public decimal Quantity { get; set; } = 1;

        public decimal UnitPrice { get; set; }

        public decimal DiscountPct { get; set; }

        public decimal LineTotal { get; set; }

        public int SortOrder { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(ProductCode)
            ? ProductName
            : $"{ProductCode} - {ProductName}";
    }

    public static class ProductInterestHelper
    {
        public static ProductInterestDTO ToDTO(this LeadProductInterest row)
            => new()
            {
                Id = row.Id,
                IdProduct = row.IdProduct,
                ProductCode = row.Product?.Code ?? string.Empty,
                ProductName = row.Product?.Name ?? string.Empty,
                Quantity = row.Quantity,
                UnitPrice = row.UnitPrice,
                DiscountPct = row.DiscountPct,
                LineTotal = row.LineTotal,
                SortOrder = row.SortOrder
            };

        public static ProductInterestDTO ToDTO(this DealProductInterest row)
            => new()
            {
                Id = row.Id,
                IdProduct = row.IdProduct,
                ProductCode = row.Product?.Code ?? string.Empty,
                ProductName = row.Product?.Name ?? string.Empty,
                Quantity = row.Quantity,
                UnitPrice = row.UnitPrice,
                DiscountPct = row.DiscountPct,
                LineTotal = row.LineTotal,
                SortOrder = row.SortOrder
            };

        public static LeadProductInterest ToLeadProductInterest(this ProductInterestDTO dto)
            => new()
            {
                Id = dto.Id,
                IdProduct = dto.IdProduct,
                Quantity = NormalizeQuantity(dto.Quantity),
                UnitPrice = dto.UnitPrice,
                DiscountPct = NormalizeDiscount(dto.DiscountPct),
                LineTotal = dto.LineTotal,
                SortOrder = dto.SortOrder
            };

        public static DealProductInterest ToDealProductInterest(this ProductInterestDTO dto)
            => new()
            {
                Id = dto.Id,
                IdProduct = dto.IdProduct,
                Quantity = NormalizeQuantity(dto.Quantity),
                UnitPrice = dto.UnitPrice,
                DiscountPct = NormalizeDiscount(dto.DiscountPct),
                LineTotal = dto.LineTotal,
                SortOrder = dto.SortOrder
            };

        public static decimal CalculateTotal(decimal quantity, decimal unitPrice, decimal discountPct)
        {
            var qty = NormalizeQuantity(quantity);
            var discount = NormalizeDiscount(discountPct);
            return Math.Round(Math.Max(0, qty * unitPrice * (1 - discount / 100m)), 2);
        }

        private static decimal NormalizeQuantity(decimal quantity) => quantity <= 0 ? 1 : quantity;

        private static decimal NormalizeDiscount(decimal discountPct) => Math.Clamp(discountPct, 0, 100);
    }
}
