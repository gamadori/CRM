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
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

       
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

        protected override async Task OnInitializedAsync()
        {
            
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

            var tikets = await _serviceTicket.GetList(filter);

            _tickets = new List<TicketSchedulerViewModel>();

            if (tikets != null)
            {
                foreach (var t in tikets.Items)
                {
                    var user = await _serviceUser.Get(t.IdUserAssigned);

                    if (user != null)
                        backColor = user.Color;
                    else
                        backColor = "white";

                    _tickets.Add(new TicketSchedulerViewModel()
                    {
                        Id = t.Id,
                        Date = t.Date,
                        Time = t.Time,
                        DateEnd = t.DateEnd,
                        Company = t.Company,
                        User = user?.NameComplete,
                        BackColor = backColor,
                        Description = t.Description

                    }); 
                }
            }
            _loading = false;
            StateHasChanged();
            return _tickets;
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
    }
}
