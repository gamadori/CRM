using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    public enum GanttPlanStates
    {
        Draft = 0,
        Active = 1,
        Archived = 2
    }

    /// <summary>
    /// Piano Gantt riutilizzabile o operativo. In questa fase viene usato come template associabile ai prodotti.
    /// </summary>
    public class GanttPlan
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Nome")]
        [Required(ErrorMessage = "Il campo {0} è necessario")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Descrizione")]
        public string? Description { get; set; }

        [Display(Name = "Stato")]
        public GanttPlanStates State { get; set; } = GanttPlanStates.Draft;

        [Display(Name = "Inizio")]
        public DateTime? StartDate { get; set; }

        [Display(Name = "Fine")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Avanzamento")]
        public int Progress { get; set; }

        [Display(Name = "Creato il")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Creato da")]
        [ForeignKey(nameof(UserCreate))]
        public string? IdUserCreate { get; set; }

        public virtual ApplicationUser? UserCreate { get; set; }

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();

        public virtual ICollection<GanttPhase> Phases { get; set; } = new List<GanttPhase>();
    }

    public class GanttPlanFilter : PagingParameterModel
    {
        public string? Search { get; set; }

        public GanttPlanStates? State { get; set; }
    }
}
