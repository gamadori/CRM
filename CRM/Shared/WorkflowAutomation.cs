using System;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared
{
    public enum WorkflowTrigger
    {
        LeadCreated,
        LeadQualified,
        LeadConverted,
        DealCreated,
        DealWon,
        DealLost
    }

    public enum WorkflowActionType
    {
        CreateActivity
    }

    public class WorkflowAutomation
    {
        public int Id { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Nome")]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        [Display(Name = "Evento")]
        public WorkflowTrigger Trigger { get; set; }

        [Display(Name = "Azione")]
        public WorkflowActionType ActionType { get; set; } = WorkflowActionType.CreateActivity;

        [Display(Name = "Importo minimo")]
        public decimal? MinAmount { get; set; }

        public LeadSource? LeadSource { get; set; }

        public LeadStatus? LeadStatus { get; set; }

        public DealStates? DealState { get; set; }

        public DealPhases? DealPhase { get; set; }

        [Display(Name = "Tipo attivita'")]
        public ActivityKind ActivityKind { get; set; } = ActivityKind.Task;

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Oggetto attivita'")]
        public string ActivitySubject { get; set; } = string.Empty;

        [Display(Name = "Descrizione attivita'")]
        public string? ActivityDescription { get; set; }

        [Range(0, 365)]
        [Display(Name = "Scadenza dopo giorni")]
        public int DueDays { get; set; } = 1;

        public bool AssignToOwner { get; set; } = true;

        public string? IdAssignee { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? LastRunAt { get; set; }

        public int ExecutionCount { get; set; }
    }

    public class WorkflowAutomationExecution
    {
        public int Id { get; set; }

        public int IdWorkflowAutomation { get; set; }

        public WorkflowTrigger Trigger { get; set; }

        public ActivityEntityType EntityType { get; set; }

        public int EntityId { get; set; }

        public DateTime ExecutedAt { get; set; } = DateTime.Now;

        public int? IdActivity { get; set; }

        public string? Error { get; set; }

        public virtual WorkflowAutomation? WorkflowAutomation { get; set; }

        public virtual Activity? Activity { get; set; }
    }

    public class WorkflowAutomationFilter : PagingParameterModel
    {
        public string? Search { get; set; }

        public bool? IsActive { get; set; }

        public WorkflowTrigger? Trigger { get; set; }
    }

    public class WorkflowAutomationExecutionFilter : PagingParameterModel
    {
        public int? IdWorkflowAutomation { get; set; }

        public WorkflowTrigger? Trigger { get; set; }

        public ActivityEntityType? EntityType { get; set; }

        public bool? Succeeded { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }
    }

    public class WorkflowAutomationExecutionDTO
    {
        public int Id { get; set; }

        public int IdWorkflowAutomation { get; set; }

        public string RuleName { get; set; } = string.Empty;

        public WorkflowTrigger Trigger { get; set; }

        public ActivityEntityType EntityType { get; set; }

        public int EntityId { get; set; }

        public string EntityName { get; set; } = string.Empty;

        public DateTime ExecutedAt { get; set; }

        public int? IdActivity { get; set; }

        public string ActivitySubject { get; set; } = string.Empty;

        public ActivityKind? ActivityKind { get; set; }

        public string? Error { get; set; }

        public bool Succeeded => string.IsNullOrWhiteSpace(Error);
    }
}
