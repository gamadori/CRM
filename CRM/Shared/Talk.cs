
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
    public enum TalkStates
    {
        Open,
        Suspended,
        CloseWon,
        CloseLost,
        Missing,

    }

    public enum TalkPhases
    {
        
        InitialContact,
        NeedsChecked,
        DecisionMakingPhase,
        OfferSubmitted,
        Obtained,
        Lost
    }
    public class Talk
    {
        public int Id { get; set; }

        [Display(Name = nameof(Talk.Date), ResourceType = typeof(Resources.Models.Talk))]
        public DateTime Date { get; set; }

        [Display(Name = nameof(Talk.Name), ResourceType = typeof(Resources.Models.Talk))]
        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        public string Name { get; set; }

        [Display(Name = nameof(Talk.Company), ResourceType = typeof(Resources.Models.Talk))]
        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [ForeignKey("Company")]

        public int? IdCompany { get; set; }

        [Display(Name = nameof(Talk.Contact), ResourceType = typeof(Resources.Models.Talk))]
        [ForeignKey("Contact")]
        public int? IdContact { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = nameof(Talk.Amount), ResourceType = typeof(Resources.Models.Talk))]
        public decimal Amount { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = nameof(Talk.Target), ResourceType = typeof(Resources.Models.Talk))]
        public decimal Target { get; set; }

        [Display(Name = nameof(Talk.Note), ResourceType = typeof(Resources.Models.Talk))]
        public string Note { get; set; }

        [Display(Name = nameof(Talk.State), ResourceType = typeof(Resources.Models.Talk))]
        public TalkStates State { get; set; }

        [Display(Name = nameof(Talk.Phase), ResourceType = typeof(Resources.Models.Talk))]
        public TalkPhases Phase { get; set; }

        [Display(Name = nameof(Talk.DateClosed), ResourceType = typeof(Resources.Models.Talk))]
        public DateTime DateClosed { get; set; }

        [Display(Name = nameof(Talk.IdUser), ResourceType = typeof(Resources.Models.Talk))]
        [ForeignKey("User")]
        public string IdUser { get; set; }

        public virtual Company? Company { get; set; }

        public virtual Contact? Contact { get; set; }

        public virtual ApplicationUser User { get; set; }   
    }

    public class TalkFilter: PagingParameterModel
    {
        public string? IdUser { get; set; }
    }
}
