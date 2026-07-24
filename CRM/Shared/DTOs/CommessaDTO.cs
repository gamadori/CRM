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
        public DateTime? StartDateActual { get; set; }
        public DateTime? EndDateActual { get; set; }
        public int Progress { get; set; }
        public int? BudgetHours { get; set; }
        public string? IdUserResponsible { get; set; }
        public string ResponsibleName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public int PhaseCount { get; set; }
        public int TicketCount { get; set; }
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
                StartDateActual = c.StartDateActual,
                EndDateActual = c.EndDateActual,
                Progress = c.Progress,
                BudgetHours = c.BudgetHours,
                IdUserResponsible = c.IdUserResponsible,
                ResponsibleName = c.UserResponsible != null ? c.UserResponsible.NameComplete : string.Empty,
                CreatedAt = c.CreatedAt,
                PhaseCount = c.Phases?.Count ?? 0
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
