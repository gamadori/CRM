using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CRM.Shared
{
    /// <summary>
    /// Fase-modello di un <see cref="GanttPlan"/> template. Usa durate RELATIVE (giorni): le date
    /// assolute vengono calcolate quando il template viene istanziato in una <see cref="Commessa"/>.
    /// Porta il tipo di ticket da aprire e il gruppo abilitato a eseguirla.
    /// </summary>
    public class GanttPhase
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(GanttPlan))]
        public int IdGanttPlan { get; set; }

        /// <summary>Fase padre per la gerarchia WBS (null = primo livello).</summary>
        [ForeignKey(nameof(Parent))]
        public int? ParentId { get; set; }

        [Display(Name = "Nome")]
        [Required(ErrorMessage = "Il campo {0} è necessario")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Descrizione")]
        public string? Description { get; set; }

        /// <summary>Durata in giorni lavorativi (0 per milestone). Relativa: nessuna data assoluta nel template.</summary>
        [Display(Name = "Durata (giorni)")]
        public int DurationDays { get; set; } = 1;

        public int SortOrder { get; set; }

        [Display(Name = "Milestone")]
        public bool IsMilestone { get; set; }

        /// <summary>Tipo di ticket generato quando la fase (istanziata) viene presa in carico.</summary>
        [ForeignKey(nameof(TicketType))]
        public int? IdTicketType { get; set; }

        /// <summary>Gruppo abilitato a eseguire la fase (chi PUO' farla).</summary>
        [ForeignKey(nameof(Group))]
        public int? IdGroup { get; set; }

        public string? Color { get; set; }

        public virtual GanttPlan? GanttPlan { get; set; }

        public virtual GanttPhase? Parent { get; set; }

        public virtual TicketType? TicketType { get; set; }

        public virtual Group? Group { get; set; }

        public virtual ICollection<GanttPhase> Children { get; set; } = new List<GanttPhase>();

        /// <summary>Dipendenze in cui questa fase è il successore.</summary>
        public virtual ICollection<GanttPhaseDependency> Dependencies { get; set; } = new List<GanttPhaseDependency>();
    }

    /// <summary>Dipendenza di precedenza tra fasi-modello del template.</summary>
    public class GanttPhaseDependency
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Phase))]
        public int IdPhase { get; set; }

        [ForeignKey(nameof(PredecessorPhase))]
        public int IdPredecessorPhase { get; set; }

        public int LagDays { get; set; }

        public DependencyType Type { get; set; } = DependencyType.FinishToStart;

        [JsonIgnore]
        public virtual GanttPhase? Phase { get; set; }

        [JsonIgnore]
        public virtual GanttPhase? PredecessorPhase { get; set; }
    }

    public class GanttPhaseFilter : PagingParameterModel
    {
        public int? IdGanttPlan { get; set; }
    }
}
