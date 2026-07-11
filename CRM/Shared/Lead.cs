using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    public enum LeadStatus
    {
        New,
        Contacted,
        Qualified,
        Converted,
        Lost,
        Disqualified
    }

    public enum LeadSource
    {
        Manual,
        Website,
        Email,
        Phone,
        Referral,
        Campaign,
        Social,
        Event,
        Other
    }

    public class Lead
    {
        public int Id { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Nome lead")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Azienda")]
        public string? CompanyName { get; set; }

        [Display(Name = "Ruolo")]
        public string? JobTitle { get; set; }

        [EmailAddress]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Telefono")]
        public string? Phone { get; set; }

        [Display(Name = "Fonte")]
        public LeadSource Source { get; set; } = LeadSource.Manual;

        [Display(Name = "Stato")]
        public LeadStatus Status { get; set; } = LeadStatus.New;

        [Range(0, 100)]
        [Display(Name = "Score")]
        public int Score { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = "Valore stimato")]
        public decimal EstimatedValue { get; set; }

        [Display(Name = "Chiusura prevista")]
        public DateTime? ExpectedCloseDate { get; set; }

        [Display(Name = "Note")]
        public string? Note { get; set; }

        [ForeignKey(nameof(Company))]
        public int? IdCompany { get; set; }

        [ForeignKey(nameof(Contact))]
        public int? IdContact { get; set; }

        [ForeignKey(nameof(User))]
        public string? IdUser { get; set; }

        [ForeignKey(nameof(ConvertedDeal))]
        public int? ConvertedDealId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? ConvertedAt { get; set; }

        public virtual Company? Company { get; set; }

        public virtual Contact? Contact { get; set; }

        public virtual ApplicationUser? User { get; set; }

        public virtual Deal? ConvertedDeal { get; set; }

        public virtual ICollection<LeadProductInterest> ProductInterests { get; set; } = new List<LeadProductInterest>();
    }

    public class LeadFilter : PagingParameterModel
    {
        public string? Search { get; set; }

        public string? IdUser { get; set; }

        public LeadStatus? Status { get; set; }

        public LeadSource? Source { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }
    }
}
