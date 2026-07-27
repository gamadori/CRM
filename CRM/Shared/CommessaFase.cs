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

    /// <summary>Regola con cui una fase considera concluso il lavoro collegato ai ticket.</summary>
    public enum CommessaFaseCompletionMode
    {
        /// <summary>La fase viene chiusa manualmente, i ticket sono solo tracciamento.</summary>
        Manual,
        /// <summary>La fase si completa quando tutti i ticket collegati sono chiusi.</summary>
        AllTicketsClosed,
        /// <summary>La fase si completa quando almeno un ticket collegato e' chiuso.</summary>
        AnyTicketClosed,
        /// <summary>La percentuale resta manuale anche se ci sono ticket collegati.</summary>
        ProgressManual
    }

    /// <summary>
    /// Tipo di legame di precedenza tra due fasi (standard project management).
    /// ATTENZIONE: oggi solo <see cref="FinishToStart"/> e' realmente implementato. Nessuna UI
    /// produce gli altri valori, e schedulazione, percorso critico, blocco all'avvio e propagazione
    /// delle date trattano ogni legame come FS. Gli altri valori esistono per il disegno delle frecce
    /// nel Gantt: prima di usarli vanno implementati nei quattro punti sopra.
    /// </summary>
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

        /// <summary>Determina se e come i ticket aggiornano stato e avanzamento della fase.</summary>
        public CommessaFaseCompletionMode CompletionMode { get; set; } = CommessaFaseCompletionMode.AllTicketsClosed;

        /// <summary>Se true, la presa in carico propone/apre un ticket operativo per la fase.</summary>
        public bool AutoCreateTicketOnTake { get; set; } = true;

        /// <summary>Se true, la fase richiede almeno un ticket per essere completata automaticamente.</summary>
        public bool RequiresTicket { get; set; } = true;

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

        public virtual ICollection<CommessaFaseTicketPlan> TicketPlans { get; set; } = new List<CommessaFaseTicketPlan>();
    }

    public class CommessaFaseTicketPlan
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(CommessaFase))]
        public int IdCommessaFase { get; set; }

        [ForeignKey(nameof(SourceTemplate))]
        public int? IdGanttPhaseTicketTemplate { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [ForeignKey(nameof(TicketType))]
        public int IdTicketType { get; set; }

        [ForeignKey(nameof(GroupAssigned))]
        public int? IdGroupAssigned { get; set; }

        public bool Required { get; set; } = true;

        public ProductionTicketAutoCreateMode AutoCreateMode { get; set; } = ProductionTicketAutoCreateMode.OnPhaseStart;

        public int SortOrder { get; set; }

        [ForeignKey(nameof(Ticket))]
        public int? IdTicket { get; set; }

        [JsonIgnore]
        public virtual CommessaFase? CommessaFase { get; set; }

        public virtual GanttPhaseTicketTemplate? SourceTemplate { get; set; }

        public virtual TicketType? TicketType { get; set; }

        public virtual Group? GroupAssigned { get; set; }

        public virtual Ticket? Ticket { get; set; }
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
