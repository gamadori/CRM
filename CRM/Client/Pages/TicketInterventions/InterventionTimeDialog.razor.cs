using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketInterventions
{
    public partial class InterventionTimeDialog : ComponentBase
    {
        [Inject]
        private DialogService DialogService { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public TicketInterventionTimeModel Time { get; set; }

        [Parameter]
        public bool IsNew { get; set; }

        private List<InterventionTimeType> _timeTypes = Enum.GetValues(typeof(InterventionTimeType))
            .Cast<InterventionTimeType>()
            .ToList();

        protected override void OnInitialized()
        {
            if (Time == null)
            {
                Time = new TicketInterventionTimeModel
                {
                    StartDateTime = TruncateToMinute(DateTime.Now),
                    EndDateTime = TruncateToMinute(DateTime.Now.AddHours(1)),
                    TimeType = InterventionTimeType.Work,
                    IsBillable = true
                };
            }
            else
            {
                // Garantisce che i secondi siano azzerati anche sui valori ricevuti
                Time.StartDateTime = TruncateToMinute(Time.StartDateTime);
                Time.EndDateTime = TruncateToMinute(Time.EndDateTime);
            }
        }

        /// <summary>
        /// Azzera secondi e millisecondi. Necessario perché il calcolo del tempo lato server
        /// usa DATEDIFF(minute, ...) e i secondi residui falsano i totali di lavoro/viaggio.
        /// </summary>
        private static DateTime TruncateToMinute(DateTime value)
            => new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

        private void SelectTimeType(InterventionTimeType type)
        {
            Time.TimeType = type;

            // Se non è un viaggio, rimuovi i km
            if (type != InterventionTimeType.Travel)
            {
                Time.TravelKilometers = null;
            }

            // Se è una pausa, non è fatturabile di default
            if (type == InterventionTimeType.Break)
            {
                Time.IsBillable = false;
            }
            else if (!Time.IsBillable && type != InterventionTimeType.Break)
            {
                // Se cambi da pausa ad altro, rendi fatturabile
                Time.IsBillable = true;
            }

            StateHasChanged();
        }

        private void CalculateDuration()
        {
            StateHasChanged();
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

        private string GetTimeTypeText(InterventionTimeType timeType)
        {
            var key = $"InterventionTimeType{timeType}";
            var translated = Localize[key];

            if (string.IsNullOrEmpty(translated) || translated == key)
            {
                return timeType switch
                {
                    InterventionTimeType.Work => "Lavoro",
                    InterventionTimeType.Travel => "Viaggio",
                    InterventionTimeType.Break => "Pausa",
                    _ => timeType.ToString()
                };
            }

            return translated;
        }

        private void HandleValidSubmit()
        {
            // Azzera i secondi: la UI mostra solo HH:mm ma il DatePicker conserva i secondi
            // del valore originale, falsando il calcolo del tempo lato server (DATEDIFF minute).
            Time.StartDateTime = TruncateToMinute(Time.StartDateTime);
            Time.EndDateTime = TruncateToMinute(Time.EndDateTime);

            // Valida che la data di fine sia successiva a quella di inizio
            if (Time.EndDateTime <= Time.StartDateTime)
            {
                // Mostra un messaggio di errore (potresti usare un NotificationService)
                Console.WriteLine("La data di fine deve essere successiva alla data di inizio");
                return;
            }

            DialogService.Close(Time);
        }

        private void Cancel()
        {
            DialogService.Close(null);
        }
    }
}
