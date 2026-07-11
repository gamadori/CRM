using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CRM.Shared.DTOs
{
    public class LeadDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        public string Name { get; set; } = string.Empty;

        public string? CompanyName { get; set; }

        public string? JobTitle { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? Phone { get; set; }

        public LeadSource Source { get; set; }

        public LeadStatus Status { get; set; }

        [Range(0, 100)]
        public int Score { get; set; }

        public decimal EstimatedValue { get; set; }

        public DateTime? ExpectedCloseDate { get; set; }

        public string? Note { get; set; }

        public int? IdCompany { get; set; }

        public int? IdContact { get; set; }

        public string? IdUser { get; set; }

        public int? ConvertedDealId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? ConvertedAt { get; set; }

        public string CompanyNameResolved { get; set; } = string.Empty;

        public string ContactName { get; set; } = string.Empty;

        public List<ProductInterestDTO> ProductInterests { get; set; } = new();

        public string ProductSummary => ProductInterests.Count == 0
            ? string.Empty
            : string.Join(", ", ProductInterests.OrderBy(x => x.SortOrder).Select(x => x.DisplayName));

        public string UserName { get; set; } = string.Empty;

        public int Permits { get; set; }
    }

    public static class LeadHelper
    {
        public static LeadDTO? ToDTO(this Lead? lead)
        {
            if (lead == null) return null;

            return new LeadDTO
            {
                Id = lead.Id,
                Name = lead.Name,
                CompanyName = lead.CompanyName,
                JobTitle = lead.JobTitle,
                Email = lead.Email,
                Phone = lead.Phone,
                Source = lead.Source,
                Status = lead.Status,
                Score = lead.Score,
                EstimatedValue = lead.EstimatedValue,
                ExpectedCloseDate = lead.ExpectedCloseDate,
                Note = lead.Note,
                IdCompany = lead.IdCompany,
                IdContact = lead.IdContact,
                IdUser = lead.IdUser,
                ConvertedDealId = lead.ConvertedDealId,
                CreatedAt = lead.CreatedAt,
                UpdatedAt = lead.UpdatedAt,
                ConvertedAt = lead.ConvertedAt,
                CompanyNameResolved = lead.Company?.RagioneSociale ?? lead.CompanyName ?? string.Empty,
                ContactName = lead.Contact?.NameComplete ?? string.Empty,
                ProductInterests = lead.ProductInterests
                    .OrderBy(x => x.SortOrder)
                    .Select(x => x.ToDTO())
                    .ToList(),
                UserName = lead.User?.NameComplete ?? string.Empty
            };
        }

        public static Lead? ToEntity(this LeadDTO? dto)
        {
            if (dto == null) return null;

            return new Lead
            {
                Id = dto.Id,
                Name = dto.Name,
                CompanyName = dto.CompanyName,
                JobTitle = dto.JobTitle,
                Email = dto.Email,
                Phone = dto.Phone,
                Source = dto.Source,
                Status = dto.Status,
                Score = dto.Score,
                EstimatedValue = dto.EstimatedValue,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Note = dto.Note,
                IdCompany = dto.IdCompany,
                IdContact = dto.IdContact,
                IdUser = dto.IdUser,
                ConvertedDealId = dto.ConvertedDealId,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                ConvertedAt = dto.ConvertedAt,
                ProductInterests = dto.ProductInterests
                    .OrderBy(x => x.SortOrder)
                    .Select(x => x.ToLeadProductInterest())
                    .ToList()
            };
        }
    }

    public class ConvertLeadRequest
    {
        public string? DealName { get; set; }

        public decimal? Amount { get; set; }

        public DateTime? ExpectedCloseDate { get; set; }

        public int? Probability { get; set; }

        public bool CreateCompanyWhenMissing { get; set; } = true;

        public bool CreateContactWhenMissing { get; set; } = true;
    }
}
