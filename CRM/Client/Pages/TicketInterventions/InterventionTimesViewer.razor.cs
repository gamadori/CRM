using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Linq;

namespace CRM.Client.Pages.TicketInterventions
{
    public partial class InterventionTimesViewer : ComponentBase
    {
        [Parameter]
        public List<TicketInterventionTimeModel> Times { get; set; } = new();

        // Proprietà calcolate per i riepiloghi
        private int TotalWorkMinutes => Times?.Where(t => t.TimeType == InterventionTimeType.Work).Sum(t => t.DurationMinutes) ?? 0;
        private int TotalTravelMinutes => Times?.Where(t => t.TimeType == InterventionTimeType.Travel).Sum(t => t.DurationMinutes) ?? 0;
        private int TotalBillableMinutes => Times?.Where(t => t.IsBillable).Sum(t => t.DurationMinutes) ?? 0;
        private int TotalKilometers => Times?.Where(t => t.TimeType == InterventionTimeType.Travel && t.TravelKilometers.HasValue).Sum(t => t.TravelKilometers!.Value) ?? 0;

        private string GetTimeTypeClass(InterventionTimeType timeType)
        {
            return timeType switch
            {
                InterventionTimeType.Work => "work",
                InterventionTimeType.Travel => "travel",
                InterventionTimeType.Break => "break",
                _ => ""
            };
        }

        private string GetTimeTypeIcon(InterventionTimeType timeType)
        {
            return timeType switch
            {
                InterventionTimeType.Work => "work",
                InterventionTimeType.Travel => "directions_car",
                InterventionTimeType.Break => "coffee",
                _ => "schedule"
            };
        }

        private string FormatDuration(int minutes)
        {
            if (minutes == 0)
                return "0h 0m";

            var hours = minutes / 60;
            var mins = minutes % 60;
            return $"{hours}h {mins}m";
        }
    }
}
