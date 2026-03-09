using BlazoringComponents.Scheduler;
using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Tickets
{
    public partial class SchedulerTimeline : ComponentBase
    {
        [Inject]
        IBaseRestService<ApplicationUser, UsersFilterModel, string> _serviceUser { get; set; }

        [Inject]
        ITicketsService _serviceTicket { get; set; }

        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Parameter]
        public DateTime DateCurrent
        {
            get { return _dateCurrent; }
            set
            {
                if (_dateCurrent != value)
                {
                    _dateCurrent = value;
                    DateCurrentChanged.InvokeAsync(_dateCurrent);
                }
            }
        }

        [Parameter]
        public EventCallback<DateTime> DateCurrentChanged { get; set; }

        [Parameter]
        public string? IdUserAssigned
        {
            get { return _idUser; }
            set
            {
                if (_idUser != value)
                {
                    _idUser = value;
                    IdUserAssignedChanged.InvokeAsync(_idUser);
                }
            }
        }

        [Parameter]
        public EventCallback<string?> IdUserAssignedChanged { get; set; }

        [Parameter]
        public SchedulerViewMode ViewMode { get; set; }

        [SupplyParameterFromQuery(Name = "userId")]
        public string? QueryUserId { get; set; }

        [SupplyParameterFromQuery(Name = "date")]
        public string? QueryDate { get; set; }

        private DateTime _dateCurrent = DateTime.Today;
        private List<ApplicationUser> _users = new List<ApplicationUser>();
        private List<TicketSchedulerViewModel> _allTickets = new List<TicketSchedulerViewModel>();
        private Dictionary<TimeSlot, List<TicketSchedulerViewModel>> _ticketsByTimeSlot = new();
        private List<TicketSchedulerViewModel> _ticketsWithoutTime = new List<TicketSchedulerViewModel>();
        private string? _idUser;
        private bool _loading = true;
        private Dictionary<int, List<ApplicationUser>> _ticketAssignedUsersCache = new();

        // Timeline configuration
        private TimeOnly _workDayStart = new TimeOnly(8, 0);
        private TimeOnly _workDayEnd = new TimeOnly(20, 0);
        private int _timeSlotMinutes = 30;
        private List<TimeSlot> _timeSlots = new();

        protected override async Task OnInitializedAsync()
        {
            if (!string.IsNullOrEmpty(QueryUserId))
            {
                _idUser = QueryUserId;
                IdUserAssigned = QueryUserId;
            }

            if (!string.IsNullOrEmpty(QueryDate) && DateTime.TryParse(QueryDate, out var parsedDate))
            {
                _dateCurrent = parsedDate;
                DateCurrent = parsedDate;
            }

            await LoadUsers();
            await LoadGlobalSettings();
            GenerateTimeSlots();
            await LoadTickets();
        }

        private async Task LoadGlobalSettings()
        {
            try
            {
                var settings = await RestClientService.GetFirst<GlobalSetting>(ConstHelper.GlobalSettingsPath);

                if (settings != null)
                {
                    if (settings.ScheduleTimeStart.HasValue)
                        _workDayStart = settings.ScheduleTimeStart.Value;

                    if (settings.ScheduleTimeEnd.HasValue)
                        _workDayEnd = settings.ScheduleTimeEnd.Value;
                }
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

        private async Task<List<ApplicationUser>> LoadUsers()
        {
            UsersFilterModel request = new UsersFilterModel();
            var response = await _serviceUser.Get(request);
            _users = response.Items.ToList();
            return _users;
        }

        private async Task LoadTickets()
        {
            _loading = true;
            _allTickets.Clear();
            _ticketsByTimeSlot.Clear();
            _ticketsWithoutTime.Clear();
            _ticketAssignedUsersCache.Clear();

            TicketFilter filter = new TicketFilter();
            if (_idUser != null && _idUser.Length > 0)
            {
                filter.IdUserAssigned = _idUser;
            }
            filter.ViewNotAssigned = ViewMode == SchedulerViewMode.Calendar;

            // Carica solo i ticket del giorno selezionato
            filter.DateFrom = _dateCurrent.Date;
            filter.DateTo = _dateCurrent.Date.AddDays(1).AddSeconds(-1);

            var tickets = await _serviceTicket.GetList(filter);

            if (tickets != null)
            {
                foreach (var t in tickets.Items)
                {
                    var assignedUsers = await LoadAssignedUsersForTicket(t.Id);

                    string backColor = assignedUsers.Any()
                        ? assignedUsers.First().Color ?? "white"
                        : "white";

                    var ticketViewModel = new TicketSchedulerViewModel
                    {
                        Id = t.Id,
                        Date = t.Date,
                        Time = t.Time,
                        DateEnd = t.DateEnd,
                        Company = t.Company,
                        User = assignedUsers.Any() ? assignedUsers.First().NameComplete : null,
                        AssignedUserIds = assignedUsers.Select(u => u.Id).ToList(),
                        AssignedUserNames = assignedUsers.Select(u => u.NameComplete).ToList(),
                        BackColor = backColor,
                        Description = t.Description,
                        
                        // ? NUOVO: Popola STATO del ticket
                        Status = t.IdState ?? 0,
                        Expired = t.DateExpired.HasValue && t.DateExpired.Value < DateTime.Now,
                        StatusColor = t.StateColor,
                        StatusText = t.State  // ? FIX: Usa State invece di StateDesc
                    };

                    _allTickets.Add(ticketViewModel);
                }
            }

            // Organizza i ticket per slot orari
            OrganizeTicketsByTimeSlots();

            _loading = false;
            StateHasChanged();
        }

        private void OrganizeTicketsByTimeSlots()
        {
            _ticketsByTimeSlot.Clear();
            _ticketsWithoutTime.Clear();

            Console.WriteLine($"?? Organizing {_allTickets.Count} tickets into time slots");

            foreach (var ticket in _allTickets)
            {
                if (ticket.Time.HasValue)
                {
                    Console.WriteLine($"?? Ticket #{ticket.Id} has time: {ticket.Time.Value:HH:mm}");

                    // ? FIX: Trova lo slot orario con confronto corretto
                    // Un ticket alle 08:15 deve stare nello slot 08:00-08:30
                    var slot = _timeSlots.FirstOrDefault(s =>
                        ticket.Time.Value >= s.Start && ticket.Time.Value < s.End);

                    if (slot != null)
                    {
                        Console.WriteLine($"   ? Assigned to slot {slot.Start:HH:mm}-{slot.End:HH:mm}");

                        if (!_ticketsByTimeSlot.ContainsKey(slot))
                        {
                            _ticketsByTimeSlot[slot] = new List<TicketSchedulerViewModel>();
                        }

                        _ticketsByTimeSlot[slot].Add(ticket);
                    }
                    else
                    {
                        Console.WriteLine($"   ?? Time {ticket.Time.Value:HH:mm} is outside working hours ({_workDayStart:HH:mm}-{_workDayEnd:HH:mm})");
                        // Fuori orario lavorativo
                        _ticketsWithoutTime.Add(ticket);
                    }
                }
                else
                {
                    Console.WriteLine($"?? Ticket #{ticket.Id} has NO time");
                    // Ticket senza orario
                    _ticketsWithoutTime.Add(ticket);
                }
            }

            Console.WriteLine($"? Organized into {_ticketsByTimeSlot.Count} time slots + {_ticketsWithoutTime.Count} without time");
        }

        private async Task<List<ApplicationUser>> LoadAssignedUsersForTicket(int ticketId)
        {
            if (_ticketAssignedUsersCache.TryGetValue(ticketId, out var cachedUsers))
            {
                return cachedUsers;
            }

            try
            {
                var userIds = await HttpClient.GetFromJsonAsync<List<string>>($"api/Tickets/{ticketId}/assigned-users");

                if (userIds == null || !userIds.Any())
                {
                    _ticketAssignedUsersCache[ticketId] = new List<ApplicationUser>();
                    return _ticketAssignedUsersCache[ticketId];
                }

                var users = _users.Where(u => userIds.Contains(u.Id)).ToList();

                var missingUserIds = userIds.Where(id => !_users.Any(u => u.Id == id)).ToList();
                foreach (var userId in missingUserIds)
                {
                    var user = await _serviceUser.Get(userId);
                    if (user != null)
                    {
                        users.Add(user);
                        _users.Add(user);
                    }
                }

                _ticketAssignedUsersCache[ticketId] = users;
                return users;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti assegnati per ticket {ticketId}: {ex.Message}");
                _ticketAssignedUsersCache[ticketId] = new List<ApplicationUser>();
                return _ticketAssignedUsersCache[ticketId];
            }
        }

        public async Task RefreshTimeline()
        {
            await LoadTickets();
        }

        private async Task PreviousDay()
        {
            DateCurrent = DateCurrent.AddDays(-1);
            await LoadTickets();
        }

        private async Task NextDay()
        {
            DateCurrent = DateCurrent.AddDays(1);
            await LoadTickets();
        }

        private async Task Today()
        {
            DateCurrent = DateTime.Today;
            await LoadTickets();
        }

        private async Task OnChangeIdUser()
        {
            await LoadTickets();
        }

        private void GoBackToScheduler()
        {
            NavigationManager.NavigateTo("/Tickets/Schedule");
        }

        private async Task NewTicket(TimeOnly? time = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "Date", _dateCurrent },
                { "OnClickCancel", new Action(CloseDialog) },
                { "OnClickSave", new Action(CloseDialog) },
                { "Scheduler", false },
                { "PageMode", PageModality.Dialog }
            };

            await DialogService.OpenAsync<Edit>(
                Localize["New Ticket"],
                parameters,
                new DialogOptions { Height = "auto", Width = "100%", Top = "0px" }
            );

            await LoadTickets();
        }

        private void CloseDialog()
        {
            DialogService.Close();
        }

        // Classe helper per slot temporali
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
