using AGUtility.Extensions;
using BlazoringComponents.Helpers;
using BlazoringComponents.Models;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using QLNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazoringComponents.Scheduler
{
    public partial class AGDayTimelineScheduler<TItem> : ComponentBase
    {
        [CascadingParameter(Name = "DateCurrent")]
        public DateTime DateCurrent { get; set; }

        [CascadingParameter(Name = "DateProperty")]
        public string DateProperty { get; set; }

        [CascadingParameter(Name = "DateEndProperty")]
        public string DateEndProperty { get; set; }

        [CascadingParameter(Name = "TimeProperty")]
        public string TimeProperty { get; set; }

        [CascadingParameter(Name = "UserProperty")]
        public string UserProperty { get; set; }

        [CascadingParameter(Name = "CompanyProperty")]
        public string CompanyProperty { get; set; }

        [CascadingParameter(Name = "DescriptionProperty")]
        public string DescriptionProperty { get; set; }

        [CascadingParameter(Name = "BackColorProperty")]
        public string BackColorProperty { get; set; }

        [CascadingParameter(Name = "Tickets")]
        public IEnumerable<TItem> Items { get; set; }

        [Parameter]
        public Action<string> OpenModal { get; set; }

        [Parameter]
        public EventCallback<DateTime> OnSelect { get; set; }

        private List<SchedulerTicket> _allTickets = new List<SchedulerTicket>();
        private Dictionary<TimeSlot, List<SchedulerTicket>> _ticketsByTimeSlot = new();
        private List<SchedulerTicket> _ticketsWithoutTime = new List<SchedulerTicket>();

        // Timeline configuration
        private TimeOnly _workDayStart = new TimeOnly(8, 0);
        private TimeOnly _workDayEnd = new TimeOnly(20, 0);
        private int _timeSlotMinutes = 30;
        private List<TimeSlot> _timeSlots = new();

        protected override async Task OnParametersSetAsync()
        {
            if (Items == null)
                Items = new List<TItem>();

            await LoadGlobalSettings();
            GenerateTimeSlots();
            LoadAndOrganizeTickets();

            await base.OnParametersSetAsync();
        }

        private async Task LoadGlobalSettings()
        {
            try
            {
                // Qui normalmente caricheresti le impostazioni dal servizio
                // Per ora uso valori di default
                _workDayStart = new TimeOnly(8, 0);
                _workDayEnd = new TimeOnly(20, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento GlobalSettings: {ex.Message}");
            }
        }

        private void GenerateTimeSlots()
        {
            _timeSlots.Clear();
            var current = _workDayStart;

            while (current < _workDayEnd)
            {
                var end = current.AddMinutes(_timeSlotMinutes);
                if (end > _workDayEnd)
                    end = _workDayEnd;

                _timeSlots.Add(new TimeSlot
                {
                    Start = current,
                    End = end,
                    Label = $"{current:HH:mm}"
                });

                current = end;
            }
        }

        private void LoadAndOrganizeTickets()
        {
            _allTickets.Clear();

            foreach (var item in Items)
            {
                var model = new SchedulerTicket
                {
                    Id = item.GetPropertyValue<object>("Id").ToString(),
                    DateStart = item.GetPropertyValue<DateTime>(DateProperty).Date,
                    TimeStart = item.GetPropertyValue<TimeOnly>(TimeProperty),
                    DateEnd = item.GetPropertyValue<DateTime>(DateEndProperty),
                    User = item.GetPropertyValue<string>(UserProperty),
                    Company = item.GetPropertyValue<string>(CompanyProperty),
                    Description = item.GetPropertyValue<string>(DescriptionProperty),
                    BackGroundColor = item.GetPropertyValue<string>(BackColorProperty),
                    AssignedUserNames = item.GetPropertyValueSafe<List<string>>("AssignedUserNames", new List<string>()),
                   
                };

                // Filtra solo i ticket del giorno corrente
                if (model.DateStart.Date == DateCurrent.Date || 
                    (model.DateStart.Date <= DateCurrent.Date && model.DateEnd.Date >= DateCurrent.Date))
                {
                    _allTickets.Add(model);
                }
            }

            OrganizeTicketsByTimeSlots();
        }

        private void OrganizeTicketsByTimeSlots()
        {
            _ticketsByTimeSlot.Clear();
            _ticketsWithoutTime.Clear();

            foreach (var ticket in _allTickets)
            {
                // Estrai TimeOnly dall'ora del ticket
                if (ticket.TimeStart.HasValue)
                {
                    var ticketTime = ticket.TimeStart.Value;

                    if (ticketTime != TimeOnly.MinValue) // Se ha un orario valido
                    {
                        // Trova lo slot orario corrispondente
                        var slot = _timeSlots.FirstOrDefault(s =>
                            ticketTime >= s.Start && ticketTime < s.End);

                        if (slot != null)
                        {
                            if (!_ticketsByTimeSlot.ContainsKey(slot))
                            {
                                _ticketsByTimeSlot[slot] = new List<SchedulerTicket>();
                            }

                            _ticketsByTimeSlot[slot].Add(ticket);
                        }
                        else
                        {
                            // Fuori orario lavorativo
                            _ticketsWithoutTime.Add(ticket);
                        }
                    }
                    else
                    {
                        // Ticket senza orario valido
                        _ticketsWithoutTime.Add(ticket);
                    }
                }
                else
                {
                    // Ticket senza orario
                    _ticketsWithoutTime.Add(ticket);
                }
            }
        }

        private string GetUserInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "?";

            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            }
            else if (parts.Length == 1 && parts[0].Length >= 2)
            {
                return parts[0].Substring(0, 2).ToUpper();
            }
            else if (parts.Length == 1)
            {
                return parts[0][0].ToString().ToUpper();
            }

            return "?";
        }

        public class TimeSlot
        {
            public TimeOnly Start { get; set; }
            public TimeOnly End { get; set; }
            public string Label { get; set; }

            public override bool Equals(object obj)
            {
                if (obj is TimeSlot other)
                {
                    return Start == other.Start && End == other.End;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Start, End);
            }
        }
    }
}
