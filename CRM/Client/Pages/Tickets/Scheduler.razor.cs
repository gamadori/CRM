using BlazoringComponents.Scheduler;
using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Syncfusion.Blazor.Grids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Http.Json;
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
        ITicketService _serviceTicket { get; set; }

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

        // ✅ NUOVO: Inject HttpClient per API calls
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

        private bool _loading = true;

        // ✅ NUOVO: Cache per utenti assegnati (evita chiamate duplicate)
        private Dictionary<int, List<ApplicationUser>> _ticketAssignedUsersCache = new();

        protected override async Task OnInitializedAsync()
        {
            // ✅ NUOVO: Imposta filtri da query string se presenti
            if (!string.IsNullOrEmpty(QueryUserId))
            {
                _idUser = QueryUserId;
                IdUserAssigned = QueryUserId; // Sincronizza con il parametro bound
            }

            if (!string.IsNullOrEmpty(QueryDate) && DateTime.TryParse(QueryDate, out var parsedDate))
            {
                _dateCurrent = parsedDate;
                DateCurrent = parsedDate; // Sincronizza con il parametro bound
            }
            
            await LoadUsers();

            await LoadSettings();
            
        }

        private async Task<List<TicketSchedulerViewModel>> LoadTickets()
        {
            string backColor = "white";

            _loading = true; 

            TicketFilter filter = new TicketFilter();
            if (_idUser != null && _idUser.Length > 0)
            {
                filter.IdUserAssigned = _idUser;
            }
            filter.ViewNotAssigned = ViewMode == SchedulerViewMode.Calendar;

            filter.DateFrom = _dateStart;
            filter.DateTo = _dateEnd;

            var tickets = await _serviceTicket.GetList(filter);

            _tickets = new List<TicketSchedulerViewModel>();
            _ticketAssignedUsersCache.Clear(); // Pulisci cache

            if (tickets != null)
            {
                foreach (var t in tickets.Items)
                {
                    // ✅ NUOVO: Carica TUTTI gli utenti assegnati dal database
                    var assignedUsers = await LoadAssignedUsersForTicket(t.Id);
                    
                    // Determina colore di sfondo (usa il primo utente assegnato come principale)
                    if (assignedUsers.Any())
                    {
                        var mainUser = assignedUsers.First();
                        backColor = mainUser.Color ?? "white";
                    }
                    else
                    {
                        backColor = "white";
                    }

                    _tickets.Add(new TicketSchedulerViewModel()
                    {
                        Id = t.Id,
                        Date = t.Date,
                        Time = t.Time,
                        DateEnd = t.DateEnd,
                        Company = t.Company,
                        
                        // ⚠️ LEGACY: Mantieni User per retrocompatibilità
                        User = assignedUsers.Any() ? assignedUsers.First().NameComplete : null,
                        
                        // ✅ NUOVO: Popola liste utenti multipli
                        AssignedUserIds = assignedUsers.Select(u => u.Id).ToList(),
                        AssignedUserNames = assignedUsers.Select(u => u.NameComplete).ToList(),
                        
                        BackColor = backColor,
                        Description = t.Description

                    }); 
                }
            }
            _loading = false;
            StateHasChanged();
            return _tickets;
        }

        /// <summary>
        /// ✅ NUOVO: Carica tutti gli utenti assegnati a un ticket dalla tabella TicketUserAssignments
        /// Usa cache per evitare chiamate duplicate
        /// </summary>
        private async Task<List<ApplicationUser>> LoadAssignedUsersForTicket(int ticketId)
        {
            // Controlla cache
            if (_ticketAssignedUsersCache.TryGetValue(ticketId, out var cachedUsers))
            {
                return cachedUsers;
            }

            try
            {
                // Chiamata API per recuperare ID utenti assegnati
                var userIds = await HttpClient.GetFromJsonAsync<List<string>>($"api/Tickets/{ticketId}/assigned-users");

                if (userIds == null || !userIds.Any())
                {
                    _ticketAssignedUsersCache[ticketId] = new List<ApplicationUser>();
                    return _ticketAssignedUsersCache[ticketId];
                }

                // Recupera dettagli utenti dalla lista utenti già caricata
                var users = _users.Where(u => userIds.Contains(u.Id)).ToList();

                // Se alcuni utenti non sono nella lista locale, caricali dal servizio
                var missingUserIds = userIds.Where(id => !_users.Any(u => u.Id == id)).ToList();
                foreach (var userId in missingUserIds)
                {
                    var user = await _serviceUser.Get(userId);
                    if (user != null)
                    {
                        users.Add(user);
                        _users.Add(user); // Aggiungi alla cache locale
                    }
                }

                // Salva in cache
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
            
            await DialogService.OpenAsync<Edit>(Localize["New Ticket"], new Dictionary<string, object>() { { "Date", date }, { "OnClickCancel", CloseDialog }, { "OnClickSave", CloseDialog }, { "Scheduler", false }, { "PageMode", PageModality.Dialog } },
                new DialogOptions() { Height = "auto", Width = "100%", Top="0px" });


            
            await schedulerTickets.Update();

        }

        private void  CloseDialog()
        {
            DialogService.Close();
        }

        /// <summary>
        /// ✅ NUOVO: Naviga alla vista Timeline mantenendo filtri e data corrente
        /// </summary>
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
    }
}
