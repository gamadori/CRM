using System;
using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    public enum CalendarItemSource
    {
        Activity,
        Ticket
    }

    public enum CalendarScope
    {
        Mine,
        Team,
        User
    }

    public class CalendarFilter
    {
        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public string? IdUser { get; set; }

        public CalendarScope Scope { get; set; } = CalendarScope.Mine;

        public CalendarItemSource? Source { get; set; }

        public bool IncludeActivities { get; set; } = true;

        public bool IncludeTickets { get; set; } = true;
    }

    public class CalendarItemDTO
    {
        public string Id { get; set; } = string.Empty;

        public CalendarItemSource Source { get; set; }

        public int SourceId { get; set; }

        public ActivityKind? ActivityKind { get; set; }

        public ActivityEntityType? EntityType { get; set; }

        public int? EntityId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public string Color { get; set; } = "#607d8b";

        public string Url { get; set; } = string.Empty;

        public string? IdAssignee { get; set; }

        public string AssigneeName { get; set; } = string.Empty;

        public string StateText { get; set; } = string.Empty;

        public bool IsOverdue { get; set; }

        public bool IsCompleted { get; set; }

        public bool CanComplete { get; set; }

        public int Permits { get; set; }
    }

    public class CalendarSummaryDTO
    {
        public int Overdue { get; set; }

        public int Today { get; set; }

        public int NextSevenDays { get; set; }
    }

    public class CalendarTeamMemberDTO
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    public class CalendarAgendaDTO
    {
        public CalendarSummaryDTO Summary { get; set; } = new();

        public List<CalendarItemDTO> Items { get; set; } = new();

        public bool CanViewTeam { get; set; }

        public CalendarScope EffectiveScope { get; set; } = CalendarScope.Mine;

        public List<CalendarTeamMemberDTO> TeamMembers { get; set; } = new();

        public string? ErrorMessage { get; set; }
    }
}
