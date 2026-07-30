using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CRM.Shared.DTOs
{
    public class TicketDTO
    {
        [Display(Name = nameof(Ticket.Id), ResourceType = typeof(Resources.Models.Ticket))]
        public int Id { get; set; }

        [Display(Name = nameof(Ticket.IdArticle), ResourceType = typeof(Resources.Models.Ticket))]        
        public string? Article { get; set; }

        [Display(Name = nameof(Ticket.IdProduct), ResourceType = typeof(Resources.Models.Ticket))]        
        public string Product { get; set; }

        public int? IdState { get; set; }

        [Display(Name = nameof(Ticket.IdState), ResourceType = typeof(Resources.Models.Ticket))]
        public string State { get; set; }

        
        public int IdType { get; set; }

        [Display(Name = nameof(Ticket.IdType), ResourceType = typeof(Resources.Models.Ticket))]

        public string DescType { get; set; }

        [Display(Name = nameof(Ticket.Priority), ResourceType = typeof(Resources.Models.Ticket))]
        public int Priority { get; set; }

        public int IdCompany { get; set; }
       
        [Display(Name = nameof(Ticket.IdCompany), ResourceType = typeof(Resources.Models.Ticket))]
        public string Company { get; set; }



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

        public string? OperationalSummaryUpdatedBy { get; set; }

        public string? OperationalSummaryUpdatedByName { get; set; }

        public bool IsBlocked { get; set; }

        public string? BlockReason { get; set; }

        public DateTime? BlockedAt { get; set; }

        public string? IdBlockedBy { get; set; }

        public string? BlockedByName { get; set; }

        public DateTime? BlockResolvedAt { get; set; }

        public string? IdBlockResolvedBy { get; set; }

        public string? BlockResolvedByName { get; set; }

        public string? BlockResolutionNote { get; set; }

        [Display(Name = nameof(Ticket.MinuteWork), ResourceType = typeof(Resources.Models.Ticket))]
        public int MinuteWork { get; set; }

        [Display(Name = nameof(TicketDTO.MinuteTravel), ResourceType = typeof(Resources.Models.TicketModel))]
        public int MinuteTravel { get; set; }

        /// <summary>Minuti di pausa fatturabili: normalmente zero, le pause nascono non fatturabili.</summary>
        public int MinuteBreak { get; set; }

        /// <summary>
        /// Ore del ticket: somma dei minuti fatturabili degli interventi (lavoro + viaggio + le
        /// eventuali pause marcate fatturabili). E' l'unico totale mostrato in lista e in scheda.
        /// </summary>
        [Display(Name = nameof(TicketDTO.MinuteBillableFormatted), ResourceType = typeof(Resources.Models.TicketModel))]
        public int MinuteBillable { get; set; }

        [Display(Name = nameof(TicketDTO.MinuteWorkFormatted), ResourceType = typeof(Resources.Models.TicketModel))]
        public string MinuteWorkFormatted { get; set; }

        [Display(Name = nameof(TicketDTO.MinuteTravelFormatted), ResourceType = typeof(Resources.Models.TicketModel))]
        public string MinuteTravelFormatted { get; set; }

        [Display(Name = nameof(TicketDTO.MinuteTotalFormatted), ResourceType = typeof(Resources.Models.TicketModel))]
        public string MinuteTotalFormatted { get; set; }

        [Display(Name = nameof(TicketDTO.MinuteBillableFormatted), ResourceType = typeof(Resources.Models.TicketModel))]
        public string MinuteBillableFormatted { get; set; }


        [Display(Name = nameof(Ticket.IdUserOpened), ResourceType = typeof(Resources.Models.Ticket))]
        public string IdUserOpened { get; set; }

        [Display(Name = nameof(Ticket.IdUserOpened), ResourceType = typeof(Resources.Models.Ticket))]
        public string UserOpened { get; set; }


        public string RequesterName => ContactName;

        public int IdContact { get; set; }

        [Display(Name = nameof(Ticket.IdContact), ResourceType = typeof(Resources.Models.Ticket))]
        public string ContactName { get; set; }

        [Display(Name = nameof(Ticket.IdUserAssigned), ResourceType = typeof(Resources.Models.Ticket))]
        public string IdUserAssigned { get; set; }

        
        public string UserAssigned { get; set; } = null;

        [Display(Name = nameof(Ticket.IdGroupAssigned), ResourceType = typeof(Resources.Models.Ticket))]
        public string GroupAssigned { get; set; }

        public int? IdGroupAssigned { get; set; }

        // ─── Smistamento AI ─────────────────────────────────────────────────────
        public int? AiSuggestedGroupId { get; set; }

        public string? AiSuggestedGroup { get; set; }

        public double? AiRoutingConfidence { get; set; }

        public string? AiRoutingReason { get; set; }

        public DateTime? AiRoutedAt { get; set; }

        /// <summary>Il gruppo e' stato assegnato in automatico dall'AI (confidenza sopra soglia).</summary>
        public bool AiRoutingApplied { get; set; }

        public AiRoutingOutcome AiRoutingOutcome { get; set; }

        /// <summary>C'e' un suggerimento in attesa di decisione: il ticket e' ancora senza gruppo.</summary>
        public bool HasPendingAiSuggestion =>
            AiRoutingOutcome == AiRoutingOutcome.Pending && AiSuggestedGroupId != null && IdGroupAssigned == null;

        public bool CanClaim { get; set; }

        public bool IsAssignedToCurrentUser { get; set; }

        public bool CanManageBlock { get; set; }

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
        public string UserClosed { get; set; }

        public int? IdDeal { get; set; }

        [Display(Name = "Opportunita")]
        public string DealName { get; set; } = string.Empty;

        public int? IdCommessaFase { get; set; }

        [Display(Name = "Fase")]
        public string CommessaFaseName { get; set; } = string.Empty;

        /// <summary>Commessa a cui appartiene la fase collegata (null se il ticket non e' di produzione).</summary>
        public int? IdCommessa { get; set; }

        [Display(Name = "Commessa")]
        public string CommessaCode { get; set; } = string.Empty;

        public bool Invoiced { get; set; }

        public string StateColor { get; set; }

        public int Permits { get; set; }

        public int Rank { get; set; }

        public TicketType? TicketType { get; set; }  

    }

    public class TicketScheduleItemDTO
    {
        public int Id { get; set; }

        public string? Numero { get; set; }

        public DateTime? Date { get; set; }

        public TimeOnly? Time { get; set; }

        public DateTime? DateEnd { get; set; }

        public DateTime? DateExpired { get; set; }

        public string? Company { get; set; }

        public string? Description { get; set; }

        public int? IdState { get; set; }

        public string? State { get; set; }

        public string? StateColor { get; set; }

        public List<TicketScheduleUserDTO> AssignedUsers { get; set; } = new();
    }

    public class TicketScheduleUserDTO
    {
        public string Id { get; set; } = string.Empty;

        public string NameComplete { get; set; } = string.Empty;

        public string? Color { get; set; }
    }

    public class TicketScheduleUpdateRequest
    {
        public DateTime Date { get; set; }

        public TimeOnly? Time { get; set; }

        public DateTime? DateEnd { get; set; }
    }

}
