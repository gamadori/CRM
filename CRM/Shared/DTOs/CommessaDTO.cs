using System;
using System.Linq;

namespace CRM.Shared.DTOs
{
    public class CommessaDTO
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public int? IdOrderRow { get; set; }
        public int? IdOrder { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int? IdCompany { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int? IdProduct { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int? IdArticle { get; set; }
        public string ArticleSerial { get; set; } = string.Empty;
        public int? IdGanttPlan { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Note { get; set; }
        public CommessaStates State { get; set; }
        public int Priority { get; set; }
        public DateTime StartDatePlanned { get; set; }
        public DateTime EndDatePlanned { get; set; }
        public DateTime? ExpectedEndDate { get; set; }
        public bool IsExpectedLate => ExpectedEndDate.HasValue && ExpectedEndDate.Value.Date > EndDatePlanned.Date;
        public int ExpectedDelayDays => IsExpectedLate ? (ExpectedEndDate!.Value.Date - EndDatePlanned.Date).Days : 0;
        public DateTime? StartDateActual { get; set; }
        public DateTime? EndDateActual { get; set; }
        public int Progress { get; set; }
        public int? BudgetHours { get; set; }
        public string? IdUserResponsible { get; set; }
        public string ResponsibleName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public int PhaseCount { get; set; }
        public int TicketCount { get; set; }
        public int BlockedTicketCount { get; set; }
        public bool HasBlockingTickets => BlockedTicketCount > 0;
        public int Permits { get; set; }
    }

    public class CommessaListItemDTO
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public CommessaStates State { get; set; }
        public string Display => string.IsNullOrWhiteSpace(Code) ? (Name ?? $"#{Id}") : $"{Code} — {Name}";
    }

    /// <summary>
    /// Richiesta di produzione interna: commesse senza riga d'ordine (magazzino, prototipi,
    /// ricambi, rilavorazioni). La data obiettivo sostituisce la consegna dell'ordine come
    /// riferimento per la schedulazione all'indietro.
    /// </summary>
    public class InternalProductionRequestDTO
    {
        public int IdProduct { get; set; }

        /// <summary>Data entro cui la produzione deve essere pronta.</summary>
        public DateTime? TargetDate { get; set; }

        public int Quantity { get; set; } = 1;

        public string? Note { get; set; }

        public string? IdUserResponsible { get; set; }
    }

    public static class CommessaMapper
    {
        public static CommessaDTO? ToDTO(this Commessa? c)
        {
            if (c == null) return null;
            return new CommessaDTO
            {
                Id = c.Id,
                Code = c.Code,
                IdOrderRow = c.IdOrderRow,
                IdOrder = c.OrderRow?.IdOrder,
                OrderNumber = c.OrderRow?.Order?.Number ?? string.Empty,
                IdCompany = c.IdCompany,
                CompanyName = c.Company != null ? c.Company.RagioneSociale : string.Empty,
                IdProduct = c.IdProduct,
                ProductName = c.Product != null ? c.Product.Name : string.Empty,
                IdArticle = c.IdArticle,
                ArticleSerial = c.Article != null ? c.Article.SerialNumber : string.Empty,
                IdGanttPlan = c.IdGanttPlan,
                Name = c.Name,
                Description = c.Description,
                Note = c.Note,
                State = c.State,
                Priority = c.Priority,
                StartDatePlanned = c.StartDatePlanned,
                EndDatePlanned = c.EndDatePlanned,
                ExpectedEndDate = c.Phases != null && c.Phases.Count > 0 ? c.Phases.Max(f => f.EndDate) : c.EndDatePlanned,
                StartDateActual = c.StartDateActual,
                EndDateActual = c.EndDateActual,
                Progress = c.Progress,
                BudgetHours = c.BudgetHours,
                IdUserResponsible = c.IdUserResponsible,
                ResponsibleName = c.UserResponsible != null ? c.UserResponsible.NameComplete : string.Empty,
                CreatedAt = c.CreatedAt,
                PhaseCount = c.Phases?.Count ?? 0,
                TicketCount = c.Phases?.SelectMany(f => f.Tickets).Count() ?? 0,
                BlockedTicketCount = c.Phases?.SelectMany(f => f.Tickets).Count(t => !t.Closed && t.IsBlocked) ?? 0
            };
        }

        public static CommessaListItemDTO ToListItem(this Commessa c) => new()
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            State = c.State
        };
    }
}
