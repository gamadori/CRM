using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CRM.Shared.DTOs
{
    public class OrderDTO
    {
        public int Id { get; set; }

        [Display(Name = "Numero")]
        public string? Number { get; set; }

        [Display(Name = "Data")]
        public DateTime Date { get; set; }

        [Display(Name = "Consegna prevista")]
        public DateTime? DeliveryDate { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Azienda")]
        public int? IdCompany { get; set; }

        [Display(Name = "Contatto")]
        public int? IdContact { get; set; }

        [Display(Name = "Preventivo")]
        public int? IdQuote { get; set; }

        [Display(Name = "Trattativa")]
        public int? IdDeal { get; set; }

        [Display(Name = "Owner")]
        public string? IdUser { get; set; }

        [Display(Name = "Stato")]
        public OrderStates State { get; set; }

        [Display(Name = "Note")]
        public string? Note { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TotalDiscount { get; set; }

        public decimal TotalVat { get; set; }

        public decimal Total { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        public string ContactName { get; set; } = string.Empty;

        public string QuoteNumber { get; set; } = string.Empty;

        public string DealName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        /// <summary>Fattura generata dall'ordine, se esiste: l'ordine è allora congelato.
        /// Popolata solo dal dettaglio, come IdOrder/OrderNumber su <see cref="QuoteDTO"/>.</summary>
        public int? IdInvoice { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public int Permits { get; set; }

        public List<OrderRowDTO> Rows { get; set; } = new();

        public void Recalculate()
        {
            foreach (var r in Rows)
            {
                var (net, vat, total) = QuoteMath.Line(r.Quantity, r.UnitPrice, r.DiscountPct, r.VatRate);
                r.LineNet = net;
                r.LineVat = vat;
                r.LineTotal = total;
            }

            Subtotal = Rows.Sum(r => r.LineNet);
            TotalDiscount = Rows.Sum(r => QuoteMath.DiscountAmount(r.Quantity, r.UnitPrice, r.DiscountPct));
            TotalVat = Rows.Sum(r => r.LineVat);
            Total = Subtotal + TotalVat;
        }
    }

    public class OrderRowDTO
    {
        public int Id { get; set; }

        public int IdOrder { get; set; }

        public int? IdProduct { get; set; }

        public int? IdArticle { get; set; }

        [Display(Name = "Descrizione")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Quantita'")]
        public decimal Quantity { get; set; } = 1;

        [Display(Name = "Prezzo unitario")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Sconto %")]
        public decimal DiscountPct { get; set; }

        [Display(Name = "IVA %")]
        public decimal VatRate { get; set; }

        public int SortOrder { get; set; }

        public decimal LineNet { get; set; }

        public decimal LineVat { get; set; }

        public decimal LineTotal { get; set; }

        /// <summary>Stato di produzione della riga (MTO).</summary>
        public RowProductionStatus ProductionStatus { get; set; }

        /// <summary>True se il prodotto della riga ha un template di produzione (→ genera commesse).</summary>
        public bool ProductHasTemplate { get; set; }
    }

    public static class OrderHelper
    {
        public static OrderDTO? ToDTO(this Order order)
        {
            if (order == null) return null;

            return new OrderDTO
            {
                Id = order.Id,
                Number = order.Number,
                Date = order.Date,
                DeliveryDate = order.DeliveryDate,
                IdCompany = order.IdCompany,
                IdContact = order.IdContact,
                IdQuote = order.IdQuote,
                IdDeal = order.IdDeal,
                IdUser = order.IdUser,
                State = order.State,
                Note = order.Note,
                Subtotal = order.Subtotal,
                TotalDiscount = order.TotalDiscount,
                TotalVat = order.TotalVat,
                Total = order.Total,
                CompanyName = order.Company != null ? order.Company.RagioneSociale : string.Empty,
                ContactName = order.Contact != null ? order.Contact.NameComplete : string.Empty,
                QuoteNumber = order.Quote != null ? (order.Quote.Number ?? string.Empty) : string.Empty,
                DealName = order.Deal != null ? order.Deal.Name : string.Empty,
                UserName = order.User != null ? order.User.NameComplete : string.Empty,
                Rows = order.Rows?
                    .OrderBy(r => r.SortOrder)
                    .Select(r => r.ToDTO())
                    .ToList() ?? new List<OrderRowDTO>()
            };
        }

        public static OrderRowDTO ToDTO(this OrderRow row)
        {
            return new OrderRowDTO
            {
                Id = row.Id,
                IdOrder = row.IdOrder,
                IdProduct = row.IdProduct,
                IdArticle = row.IdArticle,
                Description = row.Description,
                Quantity = row.Quantity,
                UnitPrice = row.UnitPrice,
                DiscountPct = row.DiscountPct,
                VatRate = row.VatRate,
                SortOrder = row.SortOrder,
                LineNet = row.LineNet,
                LineVat = row.LineVat,
                LineTotal = row.LineTotal,
                ProductionStatus = row.ProductionStatus,
                ProductHasTemplate = row.Product != null && row.Product.IdGanttPlan != null
            };
        }

        public static Order ToEntity(this OrderDTO dto)
        {
            if (dto == null) return null;

            return new Order
            {
                Id = dto.Id,
                Number = dto.Number,
                Date = dto.Date,
                DeliveryDate = dto.DeliveryDate,
                IdCompany = dto.IdCompany,
                IdContact = dto.IdContact,
                IdQuote = dto.IdQuote,
                IdDeal = dto.IdDeal,
                IdUser = dto.IdUser,
                State = dto.State,
                Note = dto.Note,
                Rows = dto.Rows?
                    .Select((r, i) => r.ToEntity(i))
                    .ToList() ?? new List<OrderRow>()
            };
        }

        public static OrderRow ToEntity(this OrderRowDTO dto, int sortOrder)
        {
            return new OrderRow
            {
                Id = dto.Id,
                IdOrder = dto.IdOrder,
                IdProduct = dto.IdProduct,
                IdArticle = dto.IdArticle,
                Description = dto.Description,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                DiscountPct = dto.DiscountPct,
                VatRate = dto.VatRate,
                SortOrder = sortOrder
            };
        }
    }
}
