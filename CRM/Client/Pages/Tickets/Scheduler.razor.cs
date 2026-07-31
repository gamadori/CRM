using BlazoringComponents.Scheduler;
using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static BlazoringComponents.Scheduler.AGScheduler<CRM.Client.Models.TicketSchedulerViewModel>;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Tickets
{
    public partial class Scheduler: ComponentBase
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
        NotificationService NotificationService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

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

       
        [Parameter]
        public int? IdTicket { get; set; } = null;

        [Parameter]
        public int? IdTicketType { get; set; } = null;

        // ✅ NUOVO: Parametri da query string (per apertura da Assign.razor)
        [SupplyParameterFromQuery(Name = "userId")]
        public string? QueryUserId { get; set; }

        [SupplyParameterFromQuery(Name = "date")]
        public string? QueryDate { get; set; }


        private DateTime? _dateStart = null;

        private DateTime? _dateEnd = null;

        private DateTime _dateCurrent  = DateTime.Today;

        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private List<TicketSchedulerViewModel> _tickets;

        private BlazoringComponents.Scheduler.AGScheduler<TicketSchedulerViewModel> schedulerTickets;

        private SchedulerViews _viewMode = SchedulerViews.Month;

        private int _maxNumMonthlyTicket;

        private string? _idUser;

        /// <summary>
        /// Natura del lavoro da mostrare: null = tutti, true = solo commessa, false = solo
        /// assistenza. Il filtro e' sulla presenza del legame, non su una commessa specifica.
        /// </summary>
        private bool? _hasCommessa;

        private bool _loading = true;

        private PageHeaderModel? _pageHeader;

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();

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

            await LoadSettings();
            
        }

        private async Task<List<TicketSchedulerViewModel>> LoadTickets()
        {
            _loading = true; 

            TicketFilter filter = new TicketFilter();
            if (_idUser != null && _idUser.Length > 0)
            {
                filter.IdUserAssigned = _idUser;
            }
            filter.ViewNotAssigned = ViewMode == SchedulerViewMode.Calendar;
            filter.HasCommessa = _hasCommessa;

            filter.DateFrom = _dateStart;
            filter.DateTo = _dateEnd;

            var tickets = await _serviceTicket.GetScheduleItemsAsync(filter);
            _tickets = tickets.Select(ToSchedulerViewModel).ToList();

            _loading = false;
            StateHasChanged();
            return _tickets;
        }

        private static TicketSchedulerViewModel ToSchedulerViewModel(TicketScheduleItemDTO ticket)
        {
            var mainUser = ticket.AssignedUsers.FirstOrDefault();
            return new TicketSchedulerViewModel
            {
                Id = ticket.Id,
                Date = ticket.Date,
                Time = ticket.Time,
                DateEnd = ticket.DateEnd,
                Company = ticket.Company ?? string.Empty,
                User = mainUser?.NameComplete,
                AssignedUserIds = ticket.AssignedUsers.Select(u => u.Id).ToList(),
                AssignedUserNames = ticket.AssignedUsers.Select(u => u.NameComplete).Where(n => !string.IsNullOrWhiteSpace(n)).ToList(),
                BackColor = mainUser?.Color ?? "white",
                Description = ticket.Description ?? string.Empty,
                Status = ticket.IdState ?? 0,
                Expired = ticket.DateExpired.HasValue && ticket.DateExpired.Value < DateTime.Now,
                StatusColor = ticket.StateColor ?? string.Empty,
                StatusText = ticket.State ?? string.Empty,
                IdCommessa = ticket.IdCommessa,
                CommessaCode = ticket.CommessaCode ?? string.Empty
            };
        }

        private async Task<List<ApplicationUser>> LoadUsers()
        {
            UsersFilterModel request = new UsersFilterModel();

            request.IdTicketToAssign = IdTicket;
            request.TicketTypeToAssign = IdTicketType;

            var response = await _serviceUser.Get(request);

            _users = response.Items.ToList();
           
            return _users;
        }

        private async Task LoadSettings()
        {
            var settings = await RestClientService.GetFirst<GlobalSetting>(ConstHelper.GlobalSettingsPath); 

            if (settings != null)
                _maxNumMonthlyTicket = settings.MonthlySchedulerMaxNumTickets;
        }
      

        protected async void OnChangeIdUser()
        {
            //StateHasChanged();
            await schedulerTickets.Update();
        }

        /// <summary>Cambia la natura del lavoro mostrata e ricarica: il filtro e' applicato dal server.</summary>
        private async Task SetCommessaFilter(bool? hasCommessa)
        {
            if (_hasCommessa == hasCommessa)
                return;

            _hasCommessa = hasCommessa;
            await schedulerTickets.Update();
        }

        private void UpdatePeriod(DateTime dateStart, DateTime dateEnd, DateTime dateCurrent)
        {

            if (_dateStart != dateStart || _dateEnd != dateEnd)
            {
                _dateStart = dateStart;
                _dateEnd = dateEnd;
                _dateCurrent = dateCurrent;


            }
        }

        private async Task NewTicket(DateTime date)
        {
            
            await DialogService.OpenAsync<Edit>(Localize["New Ticket"], new Dictionary<string, object>() { { "Date", date }, { "OnClickCancel", CloseDialog }, { "OnClickSave", CloseDialog }, { "ShowPlanningPicker", false }, { "PageMode", PageModality.Dialog } },
                new DialogOptions() { Height = "auto", Width = "100%", Top="0px" });


            
            await schedulerTickets.Update();

        }

        private async Task MoveTicket(SchedulerTicketMoveArgs args)
        {
            if (args?.Ticket == null || !int.TryParse(args.Ticket.Id, out var idTicket))
                return;

            var currentDate = args.Ticket.DateStart.Date;
            var currentTime = args.Ticket.TimeStart;
            var currentEnd = args.Ticket.DateEnd;

            if (currentDate == args.Date.Date &&
                currentTime == args.Time &&
                currentEnd == args.DateEnd)
            {
                return;
            }

            var currentSchedule = FormatSchedule(currentDate, currentTime, currentEnd);
            var newSchedule = FormatSchedule(args.Date, args.Time, args.DateEnd);
            var confirmed = await DialogService.Confirm(
                $"Confermi lo spostamento del ticket #{idTicket}?\n\nDa: {currentSchedule}\nA: {newSchedule}",
                "Conferma nuova pianificazione",
                new ConfirmOptions
                {
                    OkButtonText = "Sposta ticket",
                    CancelButtonText = "Annulla"
                });

            if (confirmed != true)
                return;

            var request = new TicketScheduleUpdateRequest
            {
                Date = args.Date,
                Time = args.Time,
                DateEnd = args.DateEnd
            };

            var updated = await _serviceTicket.UpdateScheduleAsync(idTicket, request);
            if (updated)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Pianificazione", $"Ticket #{idTicket} spostato");
                await schedulerTickets.Update();
                return;
            }

            NotificationService.Notify(NotificationSeverity.Error, "Pianificazione", $"Impossibile spostare il ticket #{idTicket}");
            await schedulerTickets.Update();
        }

        private static string FormatSchedule(DateTime date, TimeOnly? time, DateTime dateEnd)
        {
            var start = time.HasValue
                ? $"{date:dd/MM/yyyy} alle {time.Value:HH\\:mm}"
                : $"{date:dd/MM/yyyy}, senza orario";

            if (dateEnd <= date.Date)
                return start;

            return $"{start} - fine {dateEnd:dd/MM/yyyy HH:mm}";
        }

        private void  CloseDialog()
        {
            DialogService.Close();
        }

        private void SwitchToTimeline()
        {
            var queryParams = new List<string>();

            if (!string.IsNullOrEmpty(_idUser))
            {
                queryParams.Add($"userId={Uri.EscapeDataString(_idUser)}");
            }

            queryParams.Add($"date={Uri.EscapeDataString(_dateCurrent.ToString("yyyy-MM-dd"))}");

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";

            NavigationManager.NavigateTo($"/Tickets/Timeline{queryString}");
        }

        private void GoToAgenda()
        {
            NavigationManager.NavigateTo("/Agenda");
        }
    }
}
