using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>
    /// Che genere di iniziativa e'. Non e' un'etichetta cosmetica: decide come si misura, e i due
    /// gruppi non vanno mai messi nella stessa classifica.
    /// </summary>
    public enum InitiativeKind
    {
        /// <summary>
        /// Fiera. GENERA relazioni che prima non esistevano, quindi il ritorno e' una domanda
        /// legittima: costo per lead, conversione, ROI.
        /// </summary>
        Fair,

        /// <summary>
        /// Giro clienti / trasferta. Tocca clienti che ci sono gia': qui il ROI e' un numero che
        /// mente (l'ordine firmato a Monaco sarebbe probabilmente arrivato lo stesso, e una visita
        /// di cortesia per definizione non rende). Si misura in costi e in cose successe, non in
        /// ritorno.
        /// </summary>
        Trip,

        Webinar,

        Conference,

        Mailing,

        Other
    }

    public enum InitiativeState
    {
        Planned,
        InProgress,
        Closed,
        Cancelled
    }

    /// <summary>
    /// A che titolo una persona fa parte dell'iniziativa.
    /// <para>
    /// Non c'e' un ruolo "responsabile": quello e' <see cref="Initiative.IdOwner"/> e sta in un
    /// posto solo. Averlo anche qui avrebbe significato due verita' che nessuno tiene allineate,
    /// e la prima volta che divergono non si sa piu' quale delle due valga.
    /// </para>
    /// <para>
    /// I valori sono fissati esplicitamente perche' sono gia' sul database: rinumerarli
    /// spostrebbe in silenzio ogni riga salvata su un ruolo diverso.
    /// </para>
    /// </summary>
    public enum InitiativeMemberRole
    {
        Participant = 1,
        Stand = 2,
        Sales = 3,
        Technical = 4,
        Logistics = 5,
        Other = 6
    }

    public enum InitiativeScheduleType
    {
        Presence,
        Travel,
        Stand,
        Setup,
        InternalTask,
        Other
    }

    /// <summary>
    /// Un'occasione che ha un periodo, un costo e un esito: una fiera, un giro clienti, un webinar.
    /// <para>
    /// Serve perche' il CRM aveva gia' tutti i pezzi - lead, opportunita', attivita', note spese,
    /// catena preventivo/ordine/fattura - ma nessun contenitore a cui appenderli: <see
    /// cref="LeadSource.Event"/> dice CHE un lead arriva da un evento, non QUALE. Senza contenitore
    /// i dati di una fiera nascono orfani e le spese di una trasferta finiscono "costo generale".
    /// </para>
    /// <para>
    /// Fiera e trasferta stanno nella stessa tabella perche' hanno la stessa forma (periodo,
    /// partecipanti, spese, attivita', relazione finale) ma hanno DUE RESOCONTI DISTINTI, per il
    /// motivo scritto su <see cref="InitiativeKind.Trip"/>. Due tabelle gemelle avrebbero
    /// significato due CRUD e due agganci alle note spese; un resoconto solo avrebbe significato
    /// mettere "Giro Germania" e "Fiera di Hannover" nella stessa colonna costo-per-lead.
    /// </para>
    /// <para>
    /// Per la trasferta il contenitore e' soprattutto un CENTRO DI COSTO e un'unita' di rimborso:
    /// la casa di una visita resta il cliente, non il viaggio. Fra sei mesi chi apre la scheda del
    /// cliente deve vedere la visita senza dover sapere di che giro faceva parte.
    /// </para>
    /// </summary>
    public class Initiative
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [MaxLength(200)]
        [Display(Name = "Nome")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Tipo")]
        public InitiativeKind Kind { get; set; } = InitiativeKind.Trip;

        [Display(Name = "Stato")]
        public InitiativeState State { get; set; } = InitiativeState.Planned;

        /// <summary>Dove: "Hannover", "Germania e Austria". Testo libero di proposito.</summary>
        [MaxLength(200)]
        [Display(Name = "Luogo")]
        public string? Location { get; set; }

        [Display(Name = "Dal")]
        public DateTime DateFrom { get; set; }

        [Display(Name = "Al")]
        public DateTime DateTo { get; set; }

        /// <summary>
        /// Budget preventivato, in valuta base. E' il termine di paragone del consuntivo, che si
        /// ricava invece dalle note spese collegate.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Budget previsto")]
        public decimal? BudgetPlanned { get; set; }

        /// <summary>Cosa ci si aspetta, scritto prima di partire.</summary>
        [Display(Name = "Obiettivo")]
        public string? Objective { get; set; }

        /// <summary>
        /// La relazione finale in prosa. L'unica parte del resoconto che si scrive a mano: tutto il
        /// resto (costi, lead, opportunita', preventivi) e' derivato e non va mai congelato in un
        /// campo, altrimenti invecchia mentre i dati sotto cambiano.
        /// </summary>
        [Display(Name = "Relazione finale")]
        public string? ClosingNotes { get; set; }

        [ForeignKey(nameof(Owner))]
        [Display(Name = "Responsabile")]
        public string? IdOwner { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ClosedAt { get; set; }

        public virtual ApplicationUser? Owner { get; set; }

        public virtual ICollection<InitiativeMember> Members { get; set; } = new List<InitiativeMember>();

        public virtual ICollection<InitiativeSchedule> Schedules { get; set; } = new List<InitiativeSchedule>();
    }

    /// <summary>
    /// Chi dei nostri fa parte dell'iniziativa. Non contiene i giorni di presenza: quella e' una
    /// pianificazione operativa e vive in <see cref="InitiativeSchedule"/>.
    /// </summary>
    [Table("InitiativeMembers")]
    public class InitiativeMember
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdInitiative { get; set; }

        [Required]
        public string IdUser { get; set; } = string.Empty;

        public InitiativeMemberRole Role { get; set; } = InitiativeMemberRole.Participant;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.Now;

        public string? AddedBy { get; set; }

        public virtual Initiative? Initiative { get; set; }

        public virtual ApplicationUser? User { get; set; }
    }

    /// <summary>
    /// Presenza o blocco operativo dell'iniziativa. Questo e' cio' che deve comparire in agenda:
    /// non una finta attivita' commerciale, ma l'impegno reale di una persona su fiera/trasferta.
    /// </summary>
    [Table("InitiativeSchedules")]
    public class InitiativeSchedule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdInitiative { get; set; }

        [Required]
        public string IdUser { get; set; } = string.Empty;

        [Display(Name = "Inizio")]
        public DateTime Start { get; set; }

        [Display(Name = "Fine")]
        public DateTime End { get; set; }

        [Display(Name = "Tipo")]
        public InitiativeScheduleType Type { get; set; } = InitiativeScheduleType.Presence;

        [MaxLength(200)]
        public string? Location { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? CreatedBy { get; set; }

        public virtual Initiative? Initiative { get; set; }

        public virtual ApplicationUser? User { get; set; }
    }

    public class InitiativeFilter : PagingParameterModel
    {
        public InitiativeKind? Kind { get; set; }

        public InitiativeState? State { get; set; }

        public string? IdOwner { get; set; }

        /// <summary>Iniziative che si sovrappongono al periodo indicato.</summary>
        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public string? Search { get; set; }
    }
}
