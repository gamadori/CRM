using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    public enum ActivityKind
    {
        Call,
        Email,
        Meeting,
        Note,
        Task
    }

    public enum ActivityState
    {
        Planned,
        Done,
        Cancelled
    }

    /// <summary>Entita' anagrafica a cui l'attivita' e' collegata (aggancio polimorfico).</summary>
    public enum ActivityEntityType
    {
        Company,
        Contact,
        Deal,
        Ticket
    }

    /// <summary>
    /// Attivita' commerciale/operativa collegata (polimorficamente) ad Azienda/Contatto/Deal/Ticket.
    /// Alimenta la timeline unificata e l'agenda con scadenze/promemoria.
    /// </summary>
    public class Activity
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Tipo")]
        public ActivityKind Kind { get; set; } = ActivityKind.Note;

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Oggetto")]
        public string Subject { get; set; } = string.Empty;

        [Display(Name = "Descrizione")]
        public string? Description { get; set; }

        [Display(Name = "Entita'")]
        public ActivityEntityType EntityType { get; set; }

        [Display(Name = "Id entita'")]
        public int EntityId { get; set; }

        [Display(Name = "Creata da")]
        [ForeignKey(nameof(User))]
        public string? IdUser { get; set; }

        [Display(Name = "Assegnata a")]
        [ForeignKey(nameof(Assignee))]
        public string? IdAssignee { get; set; }

        [Display(Name = "Scadenza")]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Completata il")]
        public DateTime? DoneDate { get; set; }

        [Display(Name = "Stato")]
        public ActivityState State { get; set; } = ActivityState.Planned;

        [Display(Name = "Promemoria")]
        public DateTime? ReminderAt { get; set; }

        public bool ReminderSent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ApplicationUser? User { get; set; }

        public virtual ApplicationUser? Assignee { get; set; }
    }

    public class ActivityFilter : PagingParameterModel
    {
        public ActivityEntityType? EntityType { get; set; }

        public int? EntityId { get; set; }

        public string? IdUser { get; set; }

        public string? IdAssignee { get; set; }

        public ActivityState? State { get; set; }

        public ActivityKind? Kind { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public string? Search { get; set; }
    }
}
