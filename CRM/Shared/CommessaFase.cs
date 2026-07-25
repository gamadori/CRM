using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CRM.Shared
{
    /// <summary>Stato operativo di una fase di commessa.</summary>
    public enum CommessaFaseStates
    {
        Pending,
        InProgress,
        Done
    }

    /// <summary>Tipo di legame di precedenza tra due fasi (standard project management).</summary>
    public enum DependencyType
    {
        /// <summary>Finish-to-Start: il successore inizia quando il predecessore finisce (default).</summary>
        FinishToStart,
        /// <summary>Start-to-Start: iniziano insieme.</summary>
        StartToStart,
        /// <summary>Finish-to-Finish: finiscono insieme.</summary>
        FinishToFinish,
        /// <summary>Start-to-Finish (raro).</summary>
        StartToFinish
    }

    /// <summary>
    /// Fase operativa di una <see cref="Commessa"/> (nodo del Gantt), copiata dal template
    /// <see cref="GanttPhase"/> all'avvio produzione. Ha date assolute, dipendenze vincolanti e
    /// genera un ticket alla presa in carico. Si chiude quando il ticket collegato viene chiuso.
    /// </summary>
    public class CommessaFase
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Commessa))]
        public int IdCommessa { get; set; }

        /// <summary>Fase padre per la gerarchia WBS (null = primo livello).</summary>
        [ForeignKey(nameof(Parent))]
        public int? ParentId { get; set; }

        [Display(Name = "Nome")]
        [Required(ErrorMessage = "Il campo {0} è necessario")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Descrizione")]
        public string? Description { get; set; }

        [Display(Name = "Inizio")]
        public DateTime StartDate { get; set; }

        [Display(Name = "Fine")]
        public DateTime EndDate { get; set; }

        /// <summary>Avanzamento 0-100. Se ci sono ticket, deriva da essi (chiusi/totali).</summary>
        [Display(Name = "Avanzamento")]
        public int Progress { get; set; }

        public int SortOrder { get; set; }

        [Display(Name = "Milestone")]
        public bool IsMilestone { get; set; }

        public string? Color { get; set; }

        [Display(Name = "Stato")]
        public CommessaFaseStates State { get; set; } = CommessaFaseStates.Pending;

        /// <summary>Tipo di ticket da aprire in presa in carico (null = fase amministrativa, chiusura manuale).</summary>
        [ForeignKey(nameof(TicketType))]
        public int? IdTicketType { get; set; }

        /// <summary>Gruppo abilitato a eseguire la fase.</summary>
        [ForeignKey(nameof(Group))]
        public int? IdGroup { get; set; }

        /// <summary>Utente che ha preso in carico la fase.</summary>
        [ForeignKey(nameof(UserTakenBy))]
        public string? IdUserTakenBy { get; set; }

        public DateTime? TakenAt { get; set; }

        /// <summary>Calcolato a runtime dal grafo dipendenze: true se sul percorso critico.</summary>
        [NotMapped]
        public bool IsCriticalPath { get; set; }

        /// <summary>Back-reference alla commessa: non serializzata per non richiudere il ciclo
        /// Commessa -> Phases -> Commessa se una fase venisse esposta come entita'.</summary>
        [JsonIgnore]
        public virtual Commessa? Commessa { get; set; }

        public virtual CommessaFase? Parent { get; set; }

        public virtual TicketType? TicketType { get; set; }

        public virtual Group? Group { get; set; }

        public virtual ApplicationUser? UserTakenBy { get; set; }

        public virtual ICollection<CommessaFase> Children { get; set; } = new List<CommessaFase>();

        /// <summary>Dipendenze in cui questa fase è il successore.</summary>
        public virtual ICollection<CommessaFaseDependency> Dependencies { get; set; } = new List<CommessaFaseDependency>();

        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }

    /// <summary>Dipendenza di precedenza tra fasi operative di commessa (vincolante).</summary>
    public class CommessaFaseDependency
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Fase))]
        public int IdFase { get; set; }

        [ForeignKey(nameof(PredecessorFase))]
        public int IdPredecessorFase { get; set; }

        public int LagDays { get; set; }

        public DependencyType Type { get; set; } = DependencyType.FinishToStart;

        [JsonIgnore]
        public virtual CommessaFase? Fase { get; set; }

        [JsonIgnore]
        public virtual CommessaFase? PredecessorFase { get; set; }
    }

    public class CommessaFaseFilter : PagingParameterModel
    {
        public int? IdCommessa { get; set; }
    }
}
