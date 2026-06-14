
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public enum DealStates
    {
        Open,
        Suspended,
        CloseWon,
        CloseLost,
        Missing,

    }

    public enum DealPhases
    {
        
        InitialContact,
        NeedsChecked,
        DecisionMakingPhase,
        OfferSubmitted,
        Obtained,
        Lost
    }
    public class Deal
    {
        public int Id { get; set; }

        [Display(Name = nameof(Deal.Date), ResourceType = typeof(Resources.Models.Deal))]
        public DateTime Date { get; set; }

        [Display(Name = nameof(Deal.Name), ResourceType = typeof(Resources.Models.Deal))]
        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        public string Name { get; set; }

        [Display(Name = nameof(Deal.Company), ResourceType = typeof(Resources.Models.Deal))]
        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [ForeignKey("Company")]

        public int? IdCompany { get; set; }

        [Display(Name = nameof(Deal.Contact), ResourceType = typeof(Resources.Models.Deal))]
        [ForeignKey("Contact")]
        public int? IdContact { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = nameof(Deal.Amount), ResourceType = typeof(Resources.Models.Deal))]
        public decimal Amount { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = nameof(Deal.Target), ResourceType = typeof(Resources.Models.Deal))]
        public decimal Target { get; set; }

        [Display(Name = nameof(Deal.Note), ResourceType = typeof(Resources.Models.Deal))]
        public string Note { get; set; }

        [Display(Name = nameof(Deal.State), ResourceType = typeof(Resources.Models.Deal))]
        public DealStates State { get; set; }

        [Display(Name = nameof(Deal.Phase), ResourceType = typeof(Resources.Models.Deal))]
        public DealPhases Phase { get; set; }

        [Display(Name = nameof(Deal.DateClosed), ResourceType = typeof(Resources.Models.Deal))]
        public DateTime DateClosed { get; set; }

        [Display(Name = nameof(Deal.IdUser), ResourceType = typeof(Resources.Models.Deal))]
        [ForeignKey("User")]
        public string IdUser { get; set; }

        public virtual Company? Company { get; set; }

        public virtual Contact? Contact { get; set; }

        public virtual ApplicationUser User { get; set; }   
    }

    public class DealFilter: PagingParameterModel
    {
        public string? IdUser { get; set; }

        public string? Search { get; set; }

        public DealStates? State { get; set; }

        public DealPhases? Phase { get; set; }
    }
}
