using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CRM.Shared.DTOs
{
    public class QuoteDTO
    {
        public int Id { get; set; }

        [Display(Name = "Numero")]
        public string? Number { get; set; }

        [Display(Name = "Data")]
        public DateTime Date { get; set; }

        [Display(Name = "Valida fino al")]
        public DateTime? ValidUntil { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Azienda")]
        public int? IdCompany { get; set; }

        [Display(Name = "Contatto")]
        public int? IdContact { get; set; }

        [Display(Name = "Trattativa")]
        public int? IdDeal { get; set; }

        [Display(Name = "Owner")]
        public string? IdUser { get; set; }

        [Display(Name = "Stato")]
        public QuoteStates State { get; set; }

        [Display(Name = "Note")]
        public string? Note { get; set; }

        [Display(Name = "Termini e condizioni")]
        public string? TermsConditions { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TotalDiscount { get; set; }

        public decimal TotalVat { get; set; }

        public decimal Total { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        public string ContactName { get; set; } = string.Empty;

        public string DealName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public int Permits { get; set; }

        public List<QuoteRowDTO> Rows { get; set; } = new();

        /// <summary>Ricalcolo lato client per anteprima live. La verita' resta il server.</summary>
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

    public class QuoteRowDTO
    {
        public int Id { get; set; }

        public int IdQuote { get; set; }

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
    }

    /// <summary>Formule di calcolo delle righe/preventivo, condivise fra client e server.</summary>
    public static class QuoteMath
    {
        public static decimal DiscountAmount(decimal qty, decimal unit, decimal discPct)
            => Math.Round(qty * unit * (discPct / 100m), 2, MidpointRounding.AwayFromZero);

        public static (decimal net, decimal vat, decimal total) Line(decimal qty, decimal unit, decimal discPct, decimal vatRate)
        {
            var gross = qty * unit;
            var net = Math.Round(gross - gross * (discPct / 100m), 2, MidpointRounding.AwayFromZero);
            var vat = Math.Round(net * (vatRate / 100m), 2, MidpointRounding.AwayFromZero);
            return (net, vat, net + vat);
        }
    }

    public static class QuoteHelper
    {
        public static QuoteDTO? ToDTO(this Quote quote)
        {
            if (quote == null) return null;

            return new QuoteDTO
            {
                Id = quote.Id,
                Number = quote.Number,
                Date = quote.Date,
                ValidUntil = quote.ValidUntil,
                IdCompany = quote.IdCompany,
                IdContact = quote.IdContact,
                IdDeal = quote.IdDeal,
                IdUser = quote.IdUser,
                State = quote.State,
                Note = quote.Note,
                TermsConditions = quote.TermsConditions,
                Subtotal = quote.Subtotal,
                TotalDiscount = quote.TotalDiscount,
                TotalVat = quote.TotalVat,
                Total = quote.Total,
                CompanyName = quote.Company != null ? quote.Company.RagioneSociale : string.Empty,
                ContactName = quote.Contact != null ? quote.Contact.NameComplete : string.Empty,
                DealName = quote.Deal != null ? quote.Deal.Name : string.Empty,
                UserName = quote.User != null ? quote.User.NameComplete : string.Empty,
                Rows = quote.Rows?
                    .OrderBy(r => r.SortOrder)
                    .Select(r => r.ToDTO())
                    .ToList() ?? new List<QuoteRowDTO>()
            };
        }

        public static QuoteRowDTO ToDTO(this QuoteRow row)
        {
            return new QuoteRowDTO
            {
                Id = row.Id,
                IdQuote = row.IdQuote,
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
                LineTotal = row.LineTotal
            };
        }

        public static Quote ToEntity(this QuoteDTO dto)
        {
            if (dto == null) return null;

            return new Quote
            {
                Id = dto.Id,
                Number = dto.Number,
                Date = dto.Date,
                ValidUntil = dto.ValidUntil,
                IdCompany = dto.IdCompany,
                IdContact = dto.IdContact,
                IdDeal = dto.IdDeal,
                IdUser = dto.IdUser,
                State = dto.State,
                Note = dto.Note,
                TermsConditions = dto.TermsConditions,
                Rows = dto.Rows?
                    .Select((r, i) => r.ToEntity(i))
                    .ToList() ?? new List<QuoteRow>()
            };
        }

        public static QuoteRow ToEntity(this QuoteRowDTO dto, int sortOrder)
        {
            return new QuoteRow
            {
                Id = dto.Id,
                IdQuote = dto.IdQuote,
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
