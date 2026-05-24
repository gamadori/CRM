using System;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared.DTOs
{
    public class DealDTO
    {
        public int Id { get; set; }

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

        public int Permits { get; set; }
    }

    public static class DealHelper
    {
        public static DealDTO? ToDTO(this Deal deal)
        {
            if (deal == null) return null;

            return new DealDTO
            {
                Id = deal.Id,
                Date = deal.Date,
                Name = deal.Name,
                IdCompany = deal.IdCompany,
                IdContact = deal.IdContact,
                Amount = deal.Amount,
                Target = deal.Target,
                Note = deal.Note,
                State = deal.State,
                Phase = deal.Phase,
                DateClosed = deal.DateClosed,
                IdUser = deal.IdUser,
                CompanyName = deal.Company != null ? deal.Company.RagioneSociale : string.Empty,
                ContactName = deal.Contact != null ? deal.Contact.NameComplete : string.Empty,
                UserName = deal.User != null ? deal.User.NameComplete : string.Empty
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
                IdCompany = dto.IdCompany,
                IdContact = dto.IdContact,
                Amount = dto.Amount,
                Target = dto.Target,
                Note = dto.Note,
                State = dto.State,
                Phase = dto.Phase,
                DateClosed = dto.DateClosed,
                IdUser = dto.IdUser
            };
        }
    }
}
