using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>
    /// Stato della commessa di produzione (una per unità/matricola in MTO).
    /// </summary>
    public enum CommessaStates
    {
        Planned,
        InProgress,
        Suspended,
        Testing,
        Completed,
        Delivered,
        Cancelled
    }

    /// <summary>
    /// Commessa di produzione: il fascicolo di lavorazione di UNA singola unità (matricola).
    /// Nasce dalla riga d'ordine ("Avvia produzione") copiando le fasi dal template del prodotto
    /// (<see cref="GanttPlan"/>). Entità dedicata ed estensibile. Perimetro aziende fail-closed.
    /// </summary>
    public class Commessa
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Codice progressivo, es. CM-2026-0001. Generato lato server.</summary>
        [Display(Name = "Codice")]
        public string? Code { get; set; }

        /// <summary>Riga d'ordine di origine (null = produzione interna/stock, senza ordine).</summary>
        [ForeignKey(nameof(OrderRow))]
        public int? IdOrderRow { get; set; }

        [Display(Name = "Azienda")]
        [ForeignKey(nameof(Company))]
        public int? IdCompany { get; set; }

        [Display(Name = "Prodotto")]
        [ForeignKey(nameof(Product))]
        public int? IdProduct { get; set; }

        /// <summary>Matricola prodotta (null finché non esiste).</summary>
        [ForeignKey(nameof(Article))]
        public int? IdArticle { get; set; }

        /// <summary>Template da cui sono state copiate le fasi (tracciabilità).</summary>
        [ForeignKey(nameof(GanttPlan))]
        public int? IdGanttPlan { get; set; }

        [Display(Name = "Nome")]
        public string? Name { get; set; }

        [Display(Name = "Descrizione")]
        public string? Description { get; set; }

        [Display(Name = "Note")]
        public string? Note { get; set; }

        [Display(Name = "Stato")]
        public CommessaStates State { get; set; } = CommessaStates.Planned;

        [Display(Name = "Priorità")]
        public int Priority { get; set; }

        [Display(Name = "Inizio pianificato")]
        public DateTime StartDatePlanned { get; set; }

        [Display(Name = "Fine pianificata")]
        public DateTime EndDatePlanned { get; set; }

        [Display(Name = "Inizio effettivo")]
        public DateTime? StartDateActual { get; set; }

        [Display(Name = "Fine effettiva")]
        public DateTime? EndDateActual { get; set; }

        /// <summary>Avanzamento 0-100, calcolato a cascata dalle fasi.</summary>
        [Display(Name = "Avanzamento")]
        public int Progress { get; set; }

        [Display(Name = "Ore a budget")]
        public int? BudgetHours { get; set; }

        [Display(Name = "Responsabile")]
        [ForeignKey(nameof(UserResponsible))]
        public string? IdUserResponsible { get; set; }

        [ForeignKey(nameof(UserCreate))]
        public string? IdUserCreate { get; set; }

        public DateTime CreatedAt { get; set; }

        [NotMapped]
        public int Permits { get; set; }

        public virtual OrderRow? OrderRow { get; set; }
        public virtual Company? Company { get; set; }
        public virtual Product? Product { get; set; }
        public virtual Article? Article { get; set; }
        public virtual GanttPlan? GanttPlan { get; set; }
        public virtual ApplicationUser? UserResponsible { get; set; }
        public virtual ApplicationUser? UserCreate { get; set; }

        /// <summary>Fasi operative della commessa.</summary>
        public virtual ICollection<CommessaFase> Phases { get; set; } = new List<CommessaFase>();
    }

    public class CommessaFilter : PagingParameterModel
    {
        public string? Search { get; set; }
        public int? IdCompany { get; set; }
        public int? IdOrderRow { get; set; }
        public int? IdOrder { get; set; }
        public string? IdUserResponsible { get; set; }
        public CommessaStates? State { get; set; }
    }
}
