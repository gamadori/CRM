using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CRM.Shared.DTOs
{
    public class InvoiceDTO
    {
        public int Id { get; set; }

        [Display(Name = "Numero")]
        public string? Number { get; set; }

        [Display(Name = "Data")]
        public DateTime Date { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Azienda")]
        public int? IdCompany { get; set; }

        [Display(Name = "Contatto")]
        public int? IdContact { get; set; }

        [Display(Name = "Ordine")]
        public int? IdOrder { get; set; }

        [Display(Name = "Owner")]
        public string? IdUser { get; set; }

        [Display(Name = "Tipo documento")]
        public string TipoDocumento { get; set; } = "TD01";

        [Display(Name = "Codice destinatario")]
        public string CodiceDestinatario { get; set; } = "0000000";

        [Display(Name = "PEC destinatario")]
        public string? PecDestinatario { get; set; }

        [Display(Name = "Causale")]
        public string? Causale { get; set; }

        [Display(Name = "Note")]
        public string? Note { get; set; }

        [Display(Name = "Stato")]
        public InvoiceStates State { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TotalVat { get; set; }

        public decimal Total { get; set; }

        public string? SdiReference { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        public string ContactName { get; set; } = string.Empty;

        public string OrderNumber { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public int Permits { get; set; }

        public List<InvoiceRowDTO> Rows { get; set; } = new();
    }

    /// <summary>
    /// Payload per l'aggiornamento mirato del codice destinatario SdI di una fattura.
    /// </summary>
    public class InvoiceRecipientDTO
    {
        public string? CodiceDestinatario { get; set; }
    }

    public class InvoiceRowDTO
    {
        public int Id { get; set; }

        public int IdInvoice { get; set; }

        public int? IdProduct { get; set; }

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

        [Display(Name = "Natura")]
        public string? Natura { get; set; }

        public int SortOrder { get; set; }

        public decimal LineNet { get; set; }

        public decimal LineVat { get; set; }

        public decimal LineTotal { get; set; }
    }

    public static class InvoiceHelper
    {
        public static InvoiceDTO? ToDTO(this Invoice invoice)
        {
            if (invoice == null) return null;

            return new InvoiceDTO
            {
                Id = invoice.Id,
                Number = invoice.Number,
                Date = invoice.Date,
                IdCompany = invoice.IdCompany,
                IdContact = invoice.IdContact,
                IdOrder = invoice.IdOrder,
                IdUser = invoice.IdUser,
                TipoDocumento = invoice.TipoDocumento,
                CodiceDestinatario = invoice.CodiceDestinatario,
                PecDestinatario = invoice.PecDestinatario,
                Causale = invoice.Causale,
                Note = invoice.Note,
                State = invoice.State,
                Subtotal = invoice.Subtotal,
                TotalVat = invoice.TotalVat,
                Total = invoice.Total,
                SdiReference = invoice.SdiReference,
                CompanyName = invoice.Company != null ? invoice.Company.RagioneSociale : string.Empty,
                ContactName = invoice.Contact != null ? invoice.Contact.NameComplete : string.Empty,
                OrderNumber = invoice.Order != null ? (invoice.Order.Number ?? string.Empty) : string.Empty,
                UserName = invoice.User != null ? invoice.User.NameComplete : string.Empty,
                Rows = invoice.Rows?
                    .OrderBy(r => r.SortOrder)
                    .Select(r => r.ToDTO())
                    .ToList() ?? new List<InvoiceRowDTO>()
            };
        }

        public static InvoiceRowDTO ToDTO(this InvoiceRow row)
        {
            return new InvoiceRowDTO
            {
                Id = row.Id,
                IdInvoice = row.IdInvoice,
                IdProduct = row.IdProduct,
                Description = row.Description,
                Quantity = row.Quantity,
                UnitPrice = row.UnitPrice,
                DiscountPct = row.DiscountPct,
                VatRate = row.VatRate,
                Natura = row.Natura,
                SortOrder = row.SortOrder,
                LineNet = row.LineNet,
                LineVat = row.LineVat,
                LineTotal = row.LineTotal
            };
        }

        public static Invoice ToEntity(this InvoiceDTO dto)
        {
            if (dto == null) return null;

            return new Invoice
            {
                Id = dto.Id,
                Number = dto.Number,
                Date = dto.Date,
                IdCompany = dto.IdCompany,
                IdContact = dto.IdContact,
                IdOrder = dto.IdOrder,
                IdUser = dto.IdUser,
                TipoDocumento = dto.TipoDocumento,
                CodiceDestinatario = dto.CodiceDestinatario,
                PecDestinatario = dto.PecDestinatario,
                Causale = dto.Causale,
                Note = dto.Note,
                State = dto.State,
                Rows = dto.Rows?
                    .Select((r, i) => r.ToEntity(i))
                    .ToList() ?? new List<InvoiceRow>()
            };
        }

        public static InvoiceRow ToEntity(this InvoiceRowDTO dto, int sortOrder)
        {
            return new InvoiceRow
            {
                Id = dto.Id,
                IdInvoice = dto.IdInvoice,
                IdProduct = dto.IdProduct,
                Description = dto.Description,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                DiscountPct = dto.DiscountPct,
                VatRate = dto.VatRate,
                Natura = dto.Natura,
                SortOrder = sortOrder
            };
        }
    }
}
