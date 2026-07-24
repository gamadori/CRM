using System;

namespace CRM.Shared.DTOs
{
    public class GanttPlanDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public GanttPlanStates State { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Progress { get; set; }
        public DateTime CreatedAt { get; set; }

        public string Display => Name;
    }

    public static class GanttPlanMapper
    {
        public static GanttPlanDTO? ToDTO(this GanttPlan? plan)
        {
            if (plan == null) return null;
            return new GanttPlanDTO
            {
                Id = plan.Id,
                Name = plan.Name,
                Description = plan.Description,
                State = plan.State,
                StartDate = plan.StartDate,
                EndDate = plan.EndDate,
                Progress = plan.Progress,
                CreatedAt = plan.CreatedAt
            };
        }

        public static GanttPlan ToEntity(this GanttPlanDTO dto) => new()
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            State = dto.State,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Progress = dto.Progress,
            CreatedAt = dto.CreatedAt
        };
    }
}
