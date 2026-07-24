using System.Collections.Generic;
using System.Linq;

namespace CRM.Shared.DTOs
{
    public class GanttPhaseDependencyDTO
    {
        public int Id { get; set; }
        public int IdPhase { get; set; }
        public int IdPredecessorPhase { get; set; }
        public int LagDays { get; set; }
        public DependencyType Type { get; set; }
    }

    public class GanttPhaseDTO
    {
        public int Id { get; set; }
        public int IdGanttPlan { get; set; }
        public int? ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DurationDays { get; set; } = 1;
        public int SortOrder { get; set; }
        public bool IsMilestone { get; set; }
        public int? IdTicketType { get; set; }
        public int? IdGroup { get; set; }
        public string? Color { get; set; }

        public List<GanttPhaseDependencyDTO> Dependencies { get; set; } = new();
    }

    public static class GanttPhaseMapper
    {
        public static GanttPhaseDTO ToDTO(this GanttPhase p) => new()
        {
            Id = p.Id,
            IdGanttPlan = p.IdGanttPlan,
            ParentId = p.ParentId,
            Name = p.Name,
            Description = p.Description,
            DurationDays = p.DurationDays,
            SortOrder = p.SortOrder,
            IsMilestone = p.IsMilestone,
            IdTicketType = p.IdTicketType,
            IdGroup = p.IdGroup,
            Color = p.Color,
            Dependencies = p.Dependencies?.Select(d => d.ToDTO()).ToList() ?? new()
        };

        public static GanttPhaseDependencyDTO ToDTO(this GanttPhaseDependency d) => new()
        {
            Id = d.Id,
            IdPhase = d.IdPhase,
            IdPredecessorPhase = d.IdPredecessorPhase,
            LagDays = d.LagDays,
            Type = d.Type
        };

        public static GanttPhase ToEntity(this GanttPhaseDTO dto) => new()
        {
            Id = dto.Id,
            IdGanttPlan = dto.IdGanttPlan,
            ParentId = dto.ParentId,
            Name = dto.Name,
            Description = dto.Description,
            DurationDays = dto.DurationDays,
            SortOrder = dto.SortOrder,
            IsMilestone = dto.IsMilestone,
            IdTicketType = dto.IdTicketType,
            IdGroup = dto.IdGroup,
            Color = dto.Color
        };
    }
}
