using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CRM.Shared.DTOs
{
    public class DealDTO
    {
        public int Id { get; set; }

        /// <summary>Attivita' da cui l'opportunita' e' nata; null se non viene da una visita.</summary>
        public int? IdActivityOrigin { get; set; }


        [Display(Name = nameof(Deal.Date), ResourceType = typeof(Resources.Models.Deal))]
        public DateTime Date { get; set; }

        [Display(Name = nameof(Deal.Name), ResourceType = typeof(Resources.Models.Deal))]
        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        public string Name { get; set; }

        [Display(Name = nameof(Deal.Company), ResourceType = typeof(Resources.Models.Deal))]
        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        public int? IdCompany { get; set; }

        [Display(Name = nameof(Deal.Contact), ResourceType = typeof(Resources.Models.Deal))]
        public int? IdContact { get; set; }

        [Display(Name = nameof(Deal.Amount), ResourceType = typeof(Resources.Models.Deal))]
        public decimal Amount { get; set; }

        [Display(Name = nameof(Deal.Target), ResourceType = typeof(Resources.Models.Deal))]
        public decimal Target { get; set; }

        [Display(Name = "Probabilita'")]
        [Range(0, 100)]
        public int Probability { get; set; }

        [Display(Name = "Chiusura prevista")]
        public DateTime? ExpectedCloseDate { get; set; }

        public decimal WeightedAmount => State switch
        {
            DealStates.CloseWon => Amount,
            DealStates.CloseLost => 0,
            _ => Math.Round(Amount * Probability / 100m, 2)
        };

        [Display(Name = nameof(Deal.Note), ResourceType = typeof(Resources.Models.Deal))]
        public string? Note { get; set; }

        [Display(Name = nameof(Deal.State), ResourceType = typeof(Resources.Models.Deal))]
        public DealStates State { get; set; }

        [Display(Name = nameof(Deal.Phase), ResourceType = typeof(Resources.Models.Deal))]
        public DealPhases Phase { get; set; }

        [Display(Name = nameof(Deal.DateClosed), ResourceType = typeof(Resources.Models.Deal))]
        public DateTime DateClosed { get; set; }

        [Display(Name = nameof(Deal.IdUser), ResourceType = typeof(Resources.Models.Deal))]
        public string? IdUser { get; set; }

        [Display(Name = nameof(Deal.Company), ResourceType = typeof(Resources.Models.Deal))]
        public string CompanyName { get; set; } = string.Empty;

        [Display(Name = nameof(Deal.Contact), ResourceType = typeof(Resources.Models.Deal))]
        public string ContactName { get; set; } = string.Empty;

        [Display(Name = nameof(Deal.IdUser), ResourceType = typeof(Resources.Models.Deal))]
        public string UserName { get; set; } = string.Empty;

        public List<ProductInterestDTO> ProductInterests { get; set; } = new();

        public string ProductSummary => ProductInterests.Count == 0
            ? string.Empty
            : string.Join(", ", ProductInterests.OrderBy(x => x.SortOrder).Select(x => x.DisplayName));

        public int Permits { get; set; }
    }

    public static class DealHelper
    {
        public static DealDTO ToDTO(this Deal deal)
        {
            if (deal == null) return null;

            return new DealDTO
            {
                Id = deal.Id,
                Date = deal.Date,
                Name = deal.Name,
                IdActivityOrigin = deal.IdActivityOrigin,
                IdCompany = deal.IdCompany,
                IdContact = deal.IdContact,
                Amount = deal.Amount,
                Target = deal.Target,
                Probability = deal.Probability,
                ExpectedCloseDate = deal.ExpectedCloseDate,
                Note = deal.Note,
                State = deal.State,
                Phase = deal.Phase,
                DateClosed = deal.DateClosed,
                IdUser = deal.IdUser,
                CompanyName = deal.Company != null ? deal.Company.RagioneSociale : string.Empty,
                ContactName = deal.Contact != null ? deal.Contact.NameComplete : string.Empty,
                UserName = deal.User != null ? deal.User.NameComplete : string.Empty,
                ProductInterests = deal.ProductInterests
                    .OrderBy(x => x.SortOrder)
                    .Select(x => x.ToDTO())
                    .ToList()
            };
        }

        public static Deal ToEntity(this DealDTO dto)
        {
            if (dto == null) return null;

            return new Deal
            {
                Id = dto.Id,
                Date = dto.Date,
                Name = dto.Name,
                IdActivityOrigin = dto.IdActivityOrigin,
                IdCompany = dto.IdCompany,
                IdContact = dto.IdContact,
                Amount = dto.Amount,
                Target = dto.Target,
                Probability = dto.Probability,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Note = dto.Note,
                State = dto.State,
                Phase = dto.Phase,
                DateClosed = dto.DateClosed,
                IdUser = dto.IdUser,
                ProductInterests = dto.ProductInterests
                    .OrderBy(x => x.SortOrder)
                    .Select(x => x.ToDealProductInterest())
                    .ToList()
            };
        }
    }
}
