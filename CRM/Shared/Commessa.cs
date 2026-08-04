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
    /// Come nasce e come si governa il piano di una commessa.
    /// </summary>
    public enum CommessaKinds
    {
        /// <summary>Fasi clonate dal template del prodotto e schedulate all'indietro dalla consegna.</summary>
        Templated,

        /// <summary>
        /// Commessa aperta: nessun template, una sola fase di lavorazione e ticket aggiunti a mano
        /// man mano che il lavoro si presenta (sviluppo software, consulenza, servizi su misura).
        /// È l'unico caso in cui una commessa può nascere senza fasi da modello: serve a distinguere
        /// "vuota per scelta" da "vuota per errore", che le guardie sull'avvio produzione rifiutano.
        /// </summary>
        Open
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

        /// <summary>Codice progressivo, es. CM-2026-0001. Generato lato server, univoco (indice filtrato).</summary>
        [Display(Name = "Codice")]
        [MaxLength(30)]
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

        /// <summary>Da modello o aperta: decide se il piano nasce dal template o si costruisce a mano.</summary>
        [Display(Name = "Tipo")]
        public CommessaKinds Kind { get; set; } = CommessaKinds.Templated;

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

        /// <summary>
        /// La consegna promessa all'avvio, scritta una volta sola e mai più toccata: né da
        /// Riprogramma né dal salvataggio della scheda. <see cref="EndDatePlanned"/> è la consegna
        /// operativa e si muove, quindi a fine lavoro confronterebbe l'effettivo con l'ultima
        /// previsione invece che con la prima — e la commessa risulterebbe sempre puntuale.
        /// Null sulle commesse nate prima della baseline: lì la promessa iniziale non esiste più.
        /// </summary>
        [Display(Name = "Consegna promessa")]
        public DateTime? EndDateBaseline { get; set; }

        [Display(Name = "Inizio effettivo")]
        public DateTime? StartDateActual { get; set; }

        [Display(Name = "Fine effettiva")]
        public DateTime? EndDateActual { get; set; }

        /// <summary>Avanzamento 0-100, calcolato a cascata dalle fasi.</summary>
        [Display(Name = "Avanzamento")]
        public int Progress { get; set; }

        [Display(Name = "Ore a budget")]
        public int? BudgetHours { get; set; }

        /// <summary>Le ore stimate all'avvio. Stesso patto di <see cref="EndDateBaseline"/>:
        /// <see cref="BudgetHours"/> resta modificabile in corsa, questa no.</summary>
        [Display(Name = "Ore stimate")]
        public int? BudgetHoursBaseline { get; set; }

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
        public CommessaKinds? Kind { get; set; }
        public bool? ExpectedLate { get; set; }
    }
}
