using System;
using System.Collections.Generic;
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

    /// <summary>Stato di consegna del promemoria di un'attivita'.</summary>
    public enum ReminderStatus
    {
        /// <summary>Promemoria in attesa di invio (non ancora tentato).</summary>
        Pending = 0,
        /// <summary>Promemoria consegnato con successo (push o email di fallback).</summary>
        Sent = 1,
        /// <summary>Ultimo tentativo di consegna fallito; verra' ritentato finche' non si esauriscono i tentativi.</summary>
        Failed = 2
    }

    /// <summary>Entita' anagrafica a cui l'attivita' e' collegata (aggancio polimorfico).</summary>
    public enum ActivityEntityType
    {
        Company,
        Contact,
        Lead,
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

        [Display(Name = "Esito")]
        public string? Outcome { get; set; }

        [Display(Name = "Relazione di chiusura")]
        public string? CompletionNotes { get; set; }

        [Display(Name = "Prossima azione")]
        public string? NextStep { get; set; }

        [Display(Name = "Completata da")]
        [ForeignKey(nameof(CompletedBy))]
        public string? IdCompletedBy { get; set; }

        [Display(Name = "Stato")]
        public ActivityState State { get; set; } = ActivityState.Planned;

        [Display(Name = "Promemoria")]
        public DateTime? ReminderAt { get; set; }

        /// <summary>Stato di consegna del promemoria (Pending/Sent/Failed).</summary>
        public ReminderStatus ReminderStatus { get; set; } = ReminderStatus.Pending;

        /// <summary>Numero di tentativi di consegna gia' effettuati.</summary>
        public int ReminderRetryCount { get; set; }

        /// <summary>Istante dell'ultimo tentativo di consegna (per il backoff tra i retry).</summary>
        public DateTime? ReminderLastAttemptAt { get; set; }

        /// <summary>Messaggio dell'ultimo errore di consegna (diagnostica).</summary>
        public string? ReminderLastError { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Email in uscita collegata: valorizzata quando l'attività rappresenta una comunicazione
        /// email registrata (Kind == Email). Permette di aprire il testo completo dalla timeline.
        /// </summary>
        [Display(Name = "Email")]
        [ForeignKey(nameof(EmailSent))]
        public int? IdEmailSent { get; set; }

        /// <summary>Email in ingresso collegata (quando l'attività rappresenta una email ricevuta e registrata).</summary>
        public int? IdInboundEmail { get; set; }

        /// <summary>
        /// Iniziativa (fiera, trasferta) durante la quale l'attivita' e' avvenuta.
        /// <para>
        /// E' un'etichetta SECONDARIA: la casa dell'attivita' resta l'entita' a cui e' agganciata
        /// (<see cref="EntityType"/>/<see cref="EntityId"/>), cioe' il cliente che si e' andati a
        /// trovare. Chi apre la scheda del cliente fra sei mesi deve vedere la visita senza dover
        /// sapere di che giro faceva parte.
        /// </para>
        /// <para>
        /// Per le trasferte questo campo e' anche l'unico ponte verso opportunita' e preventivi:
        /// si arriva ai documenti passando da <c>Deal.IdActivityOrigin</c> e
        /// <c>Quote.IdActivityOrigin</c>, senza bisogno di attribuzioni dirette. In viaggio
        /// l'attivita' c'e' sempre - eri fisicamente nella stanza - mentre in fiera spesso manca,
        /// ed e' per questo che li' l'attribuzione va invece diretta sul documento.
        /// </para>
        /// </summary>
        [ForeignKey(nameof(Initiative))]
        [Display(Name = "Iniziativa")]
        public int? IdInitiative { get; set; }

        public virtual Initiative? Initiative { get; set; }

        public virtual ApplicationUser? User { get; set; }

        public virtual ApplicationUser? Assignee { get; set; }

        public virtual ApplicationUser? CompletedBy { get; set; }

        public virtual EmailSent? EmailSent { get; set; }

        public virtual ICollection<ActivityParticipant> Participants { get; set; } = new List<ActivityParticipant>();
    }

    [Table("ActivityParticipants")]
    public class ActivityParticipant
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdActivity { get; set; }

        [Required]
        public string IdUser { get; set; } = string.Empty;

        public DateTime AddedAt { get; set; } = DateTime.Now;

        public string? AddedBy { get; set; }

        public virtual Activity? Activity { get; set; }

        public virtual ApplicationUser? User { get; set; }
    }

    public class ActivityFilter : PagingParameterModel
    {
        public ActivityEntityType? EntityType { get; set; }

        public int? EntityId { get; set; }

        public string? IdUser { get; set; }

        public string? IdAssignee { get; set; }

        public ActivityState? State { get; set; }

        public ActivityKind? Kind { get; set; }

        public int? IdInitiative { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public string? Search { get; set; }
    }
}
