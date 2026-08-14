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
        Remote = 4,
        [Display(Name = "Workshop")]
        Workshop = 5
    }

    /// <summary>
    /// Filtri della lista ticket. I valori viaggiano come interi nelle rotte
    /// (/Tickets/Index/{TypeSearch:int}): aggiungere sempre in coda, mai in mezzo.
    /// <para>
    /// Sull'assegnazione valgono due concetti distinti, da non confondere:
    /// <see cref="NotAssigned"/> e' il lavoro non ancora smistato (nessun utente E nessun
    /// gruppo), mentre <see cref="ToClaim"/> e' gia' smistato a un gruppo ma senza un
    /// responsabile individuale. Un gruppo assegnato conta come assegnazione, coerentemente
    /// con lo stato del ticket e con il contatore della dashboard.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Ogni voce porta un <see cref="DisplayAttribute"/> con il proprio nome: senza,
    /// RedGSelectEnum ripiega su Humanizer e cerca la stringa decamelizzata ("To be invoiced")
    /// invece della chiave nei resx ("ToBeInvoiced"), mostrando l'inglese in mezzo all'italiano.
    /// I nomi qui sotto devono corrispondere alle chiavi in Resources/App*.resx.
    /// </remarks>
    public enum TicketTypeSearch
    {
        [Display(Name = "All")]
        All,

        [Display(Name = "NotAssigned")]
        NotAssigned,

        [Display(Name = "Assigned")]
        Assigned,

        [Display(Name = "Expired")]
        Expired,

        [Display(Name = "Closed")]
        Closed,

        [Display(Name = "NewMessage")]
        NewMessage = 6,

        [Display(Name = "ToBeInvoiced")]
        ToBeInvoiced = 7,

        [Display(Name = "Blocked")]
        Blocked = 8,

        /// <summary>Assegnati a un gruppo e non ancora presi in carico: la coda del tecnico.</summary>
        [Display(Name = "ToClaim")]
        ToClaim = 9
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

    /// <summary>
    /// Esito del suggerimento di gruppo prodotto dall'AI. Distinguere accettato da corretto e'
    /// l'unico modo per sapere, a distanza di mesi, se lo smistamento automatico funziona.
    /// </summary>
    public enum AiRoutingOutcome
    {
        /// <summary>Smistamento AI mai eseguito su questo ticket.</summary>
        [Display(Name = "Non elaborato")]
        None = 0,

        /// <summary>Suggerimento sotto soglia, in attesa di una decisione dell'operatore.</summary>
        [Display(Name = "In attesa")]
        Pending = 1,

        /// <summary>Gruppo suggerito confermato: assegnato in automatico o accettato dall'operatore.</summary>
        [Display(Name = "Accettato")]
        Accepted = 2,

        /// <summary>L'operatore ha scelto un gruppo diverso da quello suggerito.</summary>
        [Display(Name = "Corretto")]
        Corrected = 3,

        /// <summary>Suggerimento scartato senza assegnare il gruppo proposto.</summary>
        [Display(Name = "Ignorato")]
        Dismissed = 4
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

        public bool IsBlocked { get; set; }

        [MaxLength(2000)]
        public string? BlockReason { get; set; }

        public DateTime? BlockedAt { get; set; }

        [ForeignKey(nameof(BlockedByUser))]
        public string? IdBlockedBy { get; set; }

        public DateTime? BlockResolvedAt { get; set; }

        [ForeignKey(nameof(BlockResolvedByUser))]
        public string? IdBlockResolvedBy { get; set; }

        [MaxLength(2000)]
        public string? BlockResolutionNote { get; set; }

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

        // ─── Smistamento AI verso il gruppo ─────────────────────────────────────
        /// <summary>Gruppo proposto dall'AI alla creazione del ticket (null se non ha saputo decidere).</summary>
        [ForeignKey(nameof(AiSuggestedGroup))]
        public int? AiSuggestedGroupId { get; set; }

        /// <summary>Confidenza del suggerimento, tra 0 e 1: sopra la soglia configurata il gruppo viene assegnato da solo.</summary>
        public double? AiRoutingConfidence { get; set; }

        /// <summary>Motivazione sintetica del modello, mostrata all'operatore accanto al suggerimento.</summary>
        [MaxLength(2000)]
        public string? AiRoutingReason { get; set; }

        /// <summary>Istante dello smistamento AI; null se non e' mai stato eseguito su questo ticket.</summary>
        public DateTime? AiRoutedAt { get; set; }

        /// <summary>True se il gruppo e' stato assegnato automaticamente dall'AI (confidenza sopra soglia).</summary>
        public bool AiRoutingApplied { get; set; }

        /// <summary>Esito del suggerimento: serve a misurare l'accuratezza dello smistamento nel tempo.</summary>
        public AiRoutingOutcome AiRoutingOutcome { get; set; } = AiRoutingOutcome.None;

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

        [JsonIgnore]
        public virtual Group? AiSuggestedGroup { get; set; }

        
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
        public virtual ApplicationUser? BlockedByUser { get; set; }

        [JsonIgnore]
        public virtual ApplicationUser? BlockResolvedByUser { get; set; }


        [JsonIgnore]
        public virtual Contact? Contact { get; set; }
        public virtual Deal? Deal { get; set; }

        /// <summary>
        /// Non serializzata: porterebbe a Commessa -> Phases -> Commessa, ciclo infinito in JSON.
        /// Al client serve solo <see cref="IdCommessaFase"/> e il nome, gia' proiettato nel DTO.
        /// </summary>
        [JsonIgnore]
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

        /// <summary>
        /// Presenza del legame con una commessa, non una commessa specifica: true = solo lavoro di
        /// commessa, false = solo assistenza, null = tutti.
        /// <para>
        /// Distinto da <see cref="IdCommessa"/>, che filtra su una commessa precisa.
        /// </para>
        /// </summary>
        public bool? HasCommessa { get; set; }

        public string? Search { get; set; }

        public string IdUserOpened { get; set; }

        public string IdUserAssigned { get; set; }

        public bool ViewNotAssigned { get; set; }
        public int? IdGroupAssigned { get; set; }

        public bool? NewChatMessage { get; set; }

        public bool? IsBlocked { get; set; }

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

    public class TicketBlockRequest
    {
        [Required]
        [MaxLength(2000)]
        public string Reason { get; set; } = string.Empty;
    }

    public class TicketUnblockRequest
    {
        [MaxLength(2000)]
        public string? ResolutionNote { get; set; }
    }
}
