using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public enum TicketPriorities
    {
        Low,
        Medium,
        High
    }

    public enum TypesSupport
    {
        [Display(Name ="Telefono")]
        Phone = 0,
        [Display(Name = "Web")]
        Web = 1,
        [Display(Name = "Sul Posto")]
        OnSite = 2,
        [Display(Name = "Ufficio")]
        Office = 3,
        [Display(Name = "Da Remoto")]
        Remote = 4
    }

   public enum TicketTypeSearch
    {
        All,
        NotAssigned,
        Assigned,
        Expired,
        Closed,
        Working,
        NewMessage,
        ToBeInvoiced
    }

   
    
    public enum StateOfActiovation
    {
        Disable,
        Enabled
    }

    public enum TicketCreateSteps
    {
        CompanyTicket,
    
        TypeTicket,
        ProductTicket,
        DateTicket,
        DescriptionTicket,
        Expired,
        Assign,
        DataConfirm,
        Result,
        Attachment

    }
    
    public enum Payments
    {
        Free,
        Contract,
        ForAFee
    }

    public class Ticket
    {
        public Ticket()
        {

        }

        [Display(Name = nameof(Ticket.Id), ResourceType = typeof(Resources.Models.Ticket))]
        public int Id { get; set; }

        [Display(Name = nameof(Ticket.IdArticle), ResourceType = typeof(Resources.Models.Ticket))]
        [ForeignKey("Article")]
        public int? IdArticle { get; set; }

        [Display(Name = nameof(Ticket.IdProduct), ResourceType = typeof(Resources.Models.Ticket))]
        [ForeignKey("Product")]
        public int? IdProduct { get; set; }

        [ForeignKey("State")]
        [Display(Name = nameof(Ticket.IdState), ResourceType = typeof(Resources.Models.Ticket))]
        public int? IdState { get; set; }

        [ForeignKey("TicketType")]
        [Display(Name = nameof(Ticket.IdType), ResourceType = typeof(Resources.Models.Ticket))]

        public int IdType { get; set; }

        [Display(Name = nameof(Ticket.Priority), ResourceType = typeof(Resources.Models.Ticket))]
        public int Priority { get; set; }

        [ForeignKey("Company")]
        [Display(Name = nameof(Ticket.IdCompany), ResourceType = typeof(Resources.Models.Ticket))]
        public int IdCompany { get; set; }


        [Display(Name = nameof(Ticket.DateOpened), ResourceType = typeof(Resources.Models.Ticket))]

        public DateTime DateOpened { get; set; }


        [Display(Name = nameof(Ticket.Date), ResourceType = typeof(Resources.Models.Ticket))]
        public DateTime? Date { get; set; }

        [Display(Name = nameof(Ticket.Time), ResourceType = typeof(Resources.Models.Ticket))]
        public TimeOnly? Time { get; set; }

        [Display(Name = nameof(Ticket.DateEnd), ResourceType = typeof(Resources.Models.Ticket))]
        public DateTime? DateEnd { get; set; }

        [Display(Name = nameof(Ticket.DateClosed), ResourceType = typeof(Resources.Models.Ticket))]
        public DateTime? DateClosed { get; set; }

        [Display(Name = nameof(Ticket.DateExpired), ResourceType = typeof(Resources.Models.Ticket))]
        public DateTime? DateExpired { get; set; }

        [Display(Name = nameof(Ticket.Description), ResourceType = typeof(Resources.Models.Ticket))]
        public string Description { get; set; }

        public string? OperationalSummary { get; set; }

        public DateTime? OperationalSummaryUpdatedAt { get; set; }

        [ForeignKey(nameof(OperationalSummaryUpdatedByUser))]
        public string? OperationalSummaryUpdatedBy { get; set; }

        [Display(Name = nameof(Ticket.MinuteWork), ResourceType = typeof(Resources.Models.Ticket))]
        public int MinuteWork { get; set; }

        [ForeignKey(nameof(UserOpened))]
        [Display(Name = nameof(Ticket.IdUserOpened), ResourceType = typeof(Resources.Models.Ticket))]
        public string IdUserOpened { get; set; }

        [ForeignKey(nameof(Contact))]
        [Display(Name = nameof(Ticket.IdContact), ResourceType = typeof(Resources.Models.Ticket))]
        public int? IdContact { get; set; }

        /// <summary>
        /// ⚠️ LEGACY FIELD: Utente principale assegnato (mantenuto per retrocompatibilità).
        /// Per assegnazioni multiple, usa la collection AssignedUsers.
        /// Viene sincronizzato automaticamente con il primo elemento di AssignedUsers.
        /// </summary>
        [Display(Name = nameof(Ticket.IdUserAssigned), ResourceType = typeof(Resources.Models.Ticket))]
        public string? IdUserAssigned { get; set; } = null;

        [ForeignKey("GroupAssigned")]
        [Display(Name = nameof(Ticket.IdGroupAssigned), ResourceType = typeof(Resources.Models.Ticket))]
        public int? IdGroupAssigned { get; set; }

        [Display(Name = nameof(Ticket.IdCompanyAssigned), ResourceType = typeof(Resources.Models.Ticket))]
        public int? IdCompanyAssigned { get; set; }

        public int Progress { get; set; }


        public string Numero { get; set; }

        [Display(Name = nameof(Ticket.Support), ResourceType = typeof(Resources.Models.Ticket))]
        public int Support { get; set; }

        [Display(Name = nameof(Ticket.Closed), ResourceType = typeof(Resources.Models.Ticket))]
        public bool Closed { get; set; }

        [Display(Name = nameof(Ticket.CloseDescription), ResourceType = typeof(Resources.Models.Ticket))]
        public string CloseDescription { get; set; }

        [Display(Name = nameof(Ticket.CloseNote), ResourceType = typeof(Resources.Models.Ticket))]
        public string CloseNote { get; set; }

        [Display(Name = nameof(Ticket.IdUserClosed), ResourceType = typeof(Resources.Models.Ticket))]
        public string? IdUserClosed { get; set; } = null;

       

        [ForeignKey(nameof(Deal))]
        [Display(Name = "Opportunita")]
        public int? IdDeal { get; set; }

        [ForeignKey(nameof(CommessaFase))]
        [Display(Name = "Fase")]
        public int? IdCommessaFase { get; set; }

        [Display(Name = nameof(Ticket.Invoiced), ResourceType = typeof(Resources.Models.Ticket))]
        public bool Invoiced { get; set; }
      
        public int Payment { get; set; }

        public string? DescriptionEmbedding { get; set; }

        // ─── Preavviso appuntamento (Date + Time) ───────────────────────────────
        /// <summary>Stato di consegna del preavviso sull'appuntamento (Date+Time).</summary>
        public ReminderStatus ReminderApptStatus { get; set; } = ReminderStatus.Pending;

        /// <summary>Numero di tentativi di consegna gia' effettuati per il preavviso appuntamento.</summary>
        public int ReminderApptRetryCount { get; set; }

        /// <summary>Istante dell'ultimo tentativo di consegna del preavviso appuntamento (backoff).</summary>
        public DateTime? ReminderApptLastAttemptAt { get; set; }

        // ─── Preavviso scadenza (DateExpired, solo se non chiuso) ────────────────
        /// <summary>Stato di consegna del preavviso sulla scadenza (DateExpired).</summary>
        public ReminderStatus ReminderExpiryStatus { get; set; } = ReminderStatus.Pending;

        /// <summary>Numero di tentativi di consegna gia' effettuati per il preavviso scadenza.</summary>
        public int ReminderExpiryRetryCount { get; set; }

        /// <summary>Istante dell'ultimo tentativo di consegna del preavviso scadenza (backoff).</summary>
        public DateTime? ReminderExpiryLastAttemptAt { get; set; }

        /// <summary>Messaggio dell'ultimo errore di consegna di un preavviso (diagnostica).</summary>
        public string? ReminderLastError { get; set; }

        //[DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [NotMapped]
        public int RANK { get; set; } = 0;

        [NotMapped]
        public TicketCreateSteps Step { get; set; }

        [NotMapped]
        public string StateColor { get; set; }

        [NotMapped]
        public string StateDesc { get; set; }


        [NotMapped]
        public int Permits { get; set; }

        public virtual Company? Company { get; set; }

        public virtual TicketType? TicketType { get; set; }

        public virtual Article? Article { get; set; }

        public virtual Product? Product { get; set; }

        public virtual Group? GroupAssigned { get; set; }

        
        public virtual TicketState State { get; set; }

      

        [JsonIgnore]
        public virtual ApplicationUser UserOpened { get; set; }


        [JsonIgnore]
        public virtual ICollection<TicketChat> TicketsChats { get; set; }

        [JsonIgnore]
        public virtual ICollection<TicketIntervention> TicketInterventions { get; set; }

        /// <summary>
        /// Navigation property per l'utente principale assegnato (legacy)
        /// </summary>
        [JsonIgnore]
        public virtual ApplicationUser UserAssigned { get; set; }

        [JsonIgnore]
        public virtual ApplicationUser UserClosed { get; set; }

        [JsonIgnore]
        public virtual ApplicationUser? OperationalSummaryUpdatedByUser { get; set; }


        [JsonIgnore]
        public virtual Contact? Contact { get; set; }
        public virtual Deal? Deal { get; set; }
        public virtual CommessaFase? CommessaFase { get; set; }

        /// <summary>
        /// ✅ FONTE DI VERITÀ per assegnazioni MULTIPLE: Collezione di tutti gli utenti assegnati al ticket
        /// </summary>
        [JsonIgnore]
        public virtual ICollection<TicketUserAssignment> AssignedUsers { get; set; } = new List<TicketUserAssignment>();
    }

    public class TicketFilter: PagingParameterModel
    {
        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public DateTime? DateOpenedFrom { get; set; }

        public DateTime? DateOpenedTo { get; set; }

        public DateTime? DateClosedFrom { get; set; }

        public DateTime? DateClosedTo { get; set; }

        public DateTime? DateExpiredFrom { get; set; }

        public DateTime? DateExpiredTo { get; set; }

        public int? IdCompany { get; set; }

        public int? IdProduct { get; set; }

        public int? IdArticle { get; set; }

        public int? IdDeal { get; set; }

        public int? IdCommessaFase { get; set; }

        public int? IdCommessa { get; set; }

        public string? Search { get; set; }

        public string IdUserOpened { get; set; }

        public string IdUserAssigned { get; set; }

        public bool ViewNotAssigned { get; set; }
        public int? IdGroupAssigned { get; set; }

        public bool? NewChatMessage { get; set; }

        public int TypeSearch { get; set; } = (int)TicketTypeSearch.All;
    }

    public class TicketFilter1 : PagingParameterModel
    {
        public string DateOpenedFrom { get; set; }

        public int TicketTypeSearch { get; set; }

        
    }

    public class TicketAssign
    {
        public int Id { get; set; }

        [Display(Name = "Assegnato A:")]
        [Required]
        public string IdUser { get; set; }

        [Required]
        public DateTime Date { get; set; }
    }

    public class TicketClose
    {
        public int Id { get; set; }

        [Display(Name ="Descrizione")]
        [Required]
        public string Description { get; set; }

       [Display(Name ="Note Interne")]
        public string Note { get; set; }

        [Display(Name = "Tipo Intervento")]
        public int Support { get; set; }
        [Required]
        public DateTime Date { get; set; }
    }
}
