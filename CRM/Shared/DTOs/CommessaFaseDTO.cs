using System;
using System.Collections.Generic;
using System.Linq;

namespace CRM.Shared.DTOs
{
    public class CommessaFaseDependencyDTO
    {
        public int Id { get; set; }
        public int IdFase { get; set; }
        public int IdPredecessorFase { get; set; }
        public int LagDays { get; set; }
        public DependencyType Type { get; set; }
    }

    public class CommessaFaseDTO
    {
        public int Id { get; set; }
        public int IdCommessa { get; set; }

        /// <summary>Codice della commessa di appartenenza (es. CM-2026-0002), per mostrarla nei ticket.</summary>
        public string CommessaCode { get; set; } = string.Empty;

        public int? ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Progress { get; set; }
        public int SortOrder { get; set; }
        public bool IsMilestone { get; set; }
        public string? Color { get; set; }
        public bool IsCriticalPath { get; set; }

        public CommessaFaseStates State { get; set; }
        public int? IdTicketType { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public CommessaFaseCompletionMode CompletionMode { get; set; } = CommessaFaseCompletionMode.AllTicketsClosed;
        public bool AutoCreateTicketOnTake { get; set; } = true;
        public bool RequiresTicket { get; set; } = true;
        public int? IdGroup { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string? IdUserTakenBy { get; set; }
        public string TakenByName { get; set; } = string.Empty;
        public DateTime? TakenAt { get; set; }

        public int TicketCount { get; set; }
        public int OpenTicketCount { get; set; }
        public int ClosedTicketCount { get; set; }
        public int BlockedTicketCount { get; set; }
        public int PlannedTicketCount { get; set; }
        public int PendingPlannedTicketCount { get; set; }
        public bool HasBlockingTickets => BlockedTicketCount > 0;

        /// <summary>True se l'avanzamento deriva dai ticket collegati.</summary>
        public bool ProgressFromTickets => TicketCount > 0
            && CompletionMode is CommessaFaseCompletionMode.AllTicketsClosed or CommessaFaseCompletionMode.AnyTicketClosed;

        /// <summary>
        /// True se l'utente corrente puo' prendere in carico la fase. Calcolato dal server:
        /// Standard e Client devono appartenere al gruppo abilitato della fase.
        /// </summary>
        public bool CanTake { get; set; } = true;

        public List<CommessaFaseDependencyDTO> Dependencies { get; set; } = new();
        public List<CommessaFaseTicketPlanDTO> TicketPlans { get; set; } = new();

        /// <summary>Durata in giorni (>=1; 0 per milestone).</summary>
        public int DurationDays => IsMilestone
            ? 0
            : Math.Max(1, (int)Math.Round((EndDate.Date - StartDate.Date).TotalDays) + 1);
    }

    public static class CommessaFaseMapper
    {
        public static CommessaFaseDTO ToDTO(this CommessaFase fase)
        {
            int total = fase.Tickets?.Count ?? 0;
            int closed = fase.Tickets?.Count(t => t.Closed) ?? 0;
            var blockedIds = new HashSet<int>(
                fase.Tickets?
                    .Where(t => t.IsBlocked && !t.Closed)
                    .Select(t => t.Id) ?? Enumerable.Empty<int>());
            foreach (var idTicket in fase.TicketPlans?
                         .Where(p => p.Ticket != null && p.Ticket.IsBlocked && !p.Ticket.Closed)
                         .Select(p => p.Ticket!.Id) ?? Enumerable.Empty<int>())
                blockedIds.Add(idTicket);
            int planned = fase.TicketPlans?.Count ?? 0;
            int pendingPlans = fase.TicketPlans?.Count(p => p.IdTicket == null) ?? 0;
            return new CommessaFaseDTO
            {
                Id = fase.Id,
                IdCommessa = fase.IdCommessa,
                ParentId = fase.ParentId,
                Name = fase.Name,
                Description = fase.Description,
                StartDate = fase.StartDate,
                EndDate = fase.EndDate,
                Progress = fase.Progress,
                SortOrder = fase.SortOrder,
                IsMilestone = fase.IsMilestone,
                Color = fase.Color,
                IsCriticalPath = fase.IsCriticalPath,
                State = fase.State,
                IdTicketType = fase.IdTicketType,
                TicketTypeName = fase.TicketType != null ? fase.TicketType.Desc : string.Empty,
                CompletionMode = fase.CompletionMode,
                AutoCreateTicketOnTake = fase.AutoCreateTicketOnTake,
                RequiresTicket = fase.RequiresTicket,
                IdGroup = fase.IdGroup,
                GroupName = fase.Group != null ? fase.Group.Name : string.Empty,
                IdUserTakenBy = fase.IdUserTakenBy,
                TakenByName = fase.UserTakenBy != null ? fase.UserTakenBy.NameComplete : string.Empty,
                TakenAt = fase.TakenAt,
                TicketCount = total,
                OpenTicketCount = total - closed,
                ClosedTicketCount = closed,
                BlockedTicketCount = blockedIds.Count,
                PlannedTicketCount = planned,
                PendingPlannedTicketCount = pendingPlans,
                Dependencies = fase.Dependencies?.Select(d => d.ToDTO()).ToList() ?? new(),
                TicketPlans = fase.TicketPlans?
                    .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                    .Select(p => p.ToDTO()).ToList() ?? new()
            };
        }

        public static CommessaFaseTicketPlanDTO ToDTO(this CommessaFaseTicketPlan plan) => new()
        {
            Id = plan.Id,
            IdCommessaFase = plan.IdCommessaFase,
            IdGanttPhaseTicketTemplate = plan.IdGanttPhaseTicketTemplate,
            Title = plan.Title,
            Description = plan.Description,
            IdTicketType = plan.IdTicketType,
            IdGroupAssigned = plan.IdGroupAssigned,
            Required = plan.Required,
            AutoCreateMode = plan.AutoCreateMode,
            SortOrder = plan.SortOrder,
            IdTicket = plan.IdTicket
        };

        public static CommessaFaseDependencyDTO ToDTO(this CommessaFaseDependency dep) => new()
        {
            Id = dep.Id,
            IdFase = dep.IdFase,
            IdPredecessorFase = dep.IdPredecessorFase,
            LagDays = dep.LagDays,
            Type = dep.Type
        };

        public static CommessaFase ToEntity(this CommessaFaseDTO dto) => new()
        {
            Id = dto.Id,
            IdCommessa = dto.IdCommessa,
            ParentId = dto.ParentId,
            Name = dto.Name,
            Description = dto.Description,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Progress = dto.Progress,
            SortOrder = dto.SortOrder,
            IsMilestone = dto.IsMilestone,
            Color = dto.Color,
            State = dto.State,
            IdTicketType = dto.IdTicketType,
            CompletionMode = dto.CompletionMode,
            AutoCreateTicketOnTake = dto.AutoCreateTicketOnTake,
            RequiresTicket = dto.RequiresTicket,
            IdGroup = dto.IdGroup
        };
    }

    public class CommessaFaseTicketPlanDTO
    {
        public int Id { get; set; }
        public int IdCommessaFase { get; set; }
        public int? IdGanttPhaseTicketTemplate { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int IdTicketType { get; set; }
        public int? IdGroupAssigned { get; set; }
        public bool Required { get; set; } = true;
        public ProductionTicketAutoCreateMode AutoCreateMode { get; set; } = ProductionTicketAutoCreateMode.OnPhaseStart;
        public int SortOrder { get; set; }
        public int? IdTicket { get; set; }
    }
}
