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

        /// <summary>
        /// Numero di utenti assegnati all'intervento (per calcolare i totali moltiplicati)
        /// </summary>
        [Parameter]
        public int AssignedUsersCount { get; set; } = 1;

        // Numero effettivo di utenti (minimo 1)
        private int EffectiveUsersCount => AssignedUsersCount > 0 ? AssignedUsersCount : 1;

        // Proprietà calcolate per i riepiloghi (singoli, senza moltiplicazione)
        private int BaseWorkMinutes => Times?.Where(t => t.TimeType == InterventionTimeType.Work).Sum(t => t.DurationMinutes) ?? 0;
        private int BaseTravelMinutes => Times?.Where(t => t.TimeType == InterventionTimeType.Travel).Sum(t => t.DurationMinutes) ?? 0;
        private int BaseBillableMinutes => Times?.Where(t => t.IsBillable).Sum(t => t.DurationMinutes) ?? 0;

        // Proprietà calcolate per i totali (moltiplicati per numero utenti)
        private int TotalWorkMinutes => BaseWorkMinutes * EffectiveUsersCount;
        private int TotalTravelMinutes => BaseTravelMinutes * EffectiveUsersCount;
        private int TotalBillableMinutes => BaseBillableMinutes * EffectiveUsersCount;
        
        // I km NON vanno moltiplicati (rappresentano la distanza, non il tempo)
        private int TotalKilometers => Times?.Where(t => t.TimeType == InterventionTimeType.Travel && t.TravelKilometers.HasValue).Sum(t => t.TravelKilometers!.Value) ?? 0;

        // Indica se mostrare il dettaglio della moltiplicazione
        private bool ShowMultiplier => EffectiveUsersCount > 1;

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
