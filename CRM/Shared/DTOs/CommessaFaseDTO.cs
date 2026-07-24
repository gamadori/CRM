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
        public int? IdGroup { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string? IdUserTakenBy { get; set; }
        public string TakenByName { get; set; } = string.Empty;
        public DateTime? TakenAt { get; set; }

        public int TicketCount { get; set; }
        public int OpenTicketCount { get; set; }
        public int ClosedTicketCount { get; set; }

        /// <summary>True se l'avanzamento deriva dai ticket (fase con almeno un ticket).</summary>
        public bool ProgressFromTickets => TicketCount > 0;

        public List<CommessaFaseDependencyDTO> Dependencies { get; set; } = new();

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
                IdGroup = fase.IdGroup,
                GroupName = fase.Group != null ? fase.Group.Name : string.Empty,
                IdUserTakenBy = fase.IdUserTakenBy,
                TakenByName = fase.UserTakenBy != null ? fase.UserTakenBy.NameComplete : string.Empty,
                TakenAt = fase.TakenAt,
                TicketCount = total,
                OpenTicketCount = total - closed,
                ClosedTicketCount = closed,
                Dependencies = fase.Dependencies?.Select(d => d.ToDTO()).ToList() ?? new()
            };
        }

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
            IdGroup = dto.IdGroup
        };
    }
}
