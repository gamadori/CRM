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
        public List<GanttPhaseTicketTemplateDTO> TicketTemplates { get; set; } = new();
    }

    public class GanttPhaseTicketTemplateDTO
    {
        public int Id { get; set; }
        public int IdGanttPhase { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int IdTicketType { get; set; }
        public int? IdGroupAssigned { get; set; }
        public bool Required { get; set; } = true;
        public ProductionTicketAutoCreateMode AutoCreateMode { get; set; } = ProductionTicketAutoCreateMode.OnPhaseStart;
        public int SortOrder { get; set; }
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
            Dependencies = p.Dependencies?.Select(d => d.ToDTO()).ToList() ?? new(),
            TicketTemplates = p.TicketTemplates?
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                .Select(t => t.ToDTO()).ToList() ?? new()
        };

        public static GanttPhaseTicketTemplateDTO ToDTO(this GanttPhaseTicketTemplate t) => new()
        {
            Id = t.Id,
            IdGanttPhase = t.IdGanttPhase,
            Title = t.Title,
            Description = t.Description,
            IdTicketType = t.IdTicketType,
            IdGroupAssigned = t.IdGroupAssigned,
            Required = t.Required,
            AutoCreateMode = t.AutoCreateMode,
            SortOrder = t.SortOrder
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

        public static GanttPhaseTicketTemplate ToEntity(this GanttPhaseTicketTemplateDTO dto) => new()
        {
            Id = dto.Id,
            IdGanttPhase = dto.IdGanttPhase,
            Title = dto.Title,
            Description = dto.Description,
            IdTicketType = dto.IdTicketType,
            IdGroupAssigned = dto.IdGroupAssigned,
            Required = dto.Required,
            AutoCreateMode = dto.AutoCreateMode,
            SortOrder = dto.SortOrder
        };
    }
}
