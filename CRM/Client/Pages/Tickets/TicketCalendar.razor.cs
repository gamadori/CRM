using BlazoringComponents.Models;
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
using System.Threading.Tasks;
using static BlazoringComponents.Scheduler.AGScheduler<CRM.Client.Models.TicketSchedulerViewModel>;

namespace CRM.Client.Pages.Tickets
{
    public partial class TicketCalendar: ComponentBase
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
        public string? IdUser { get; set; }

        [Parameter]
        public DateTime Date { get; set; }

        [Parameter]
        public int? IdTicket { get; set; }

        [Parameter]
        public int? IdTicketType { get; set; }

      

        private BlazoringComponents.Scheduler.AGScheduler<TicketSchedulerViewModel> _schedulerTickets;

        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private int _maxNumMonthlyTicket;

        private List<TicketSchedulerViewModel> _tickets;

        private DateTime? _dateStart = null;

        private DateTime? _dateEnd = null;

        private bool _dateLocked = false;

        private Ticket _ticket = null;

        private string _header;

        protected override async Task OnInitializedAsync()
        {
            await LoadTicket();

            await LoadSettings();

            if (_ticket.Id > 0)
                _header = $"{Localize["Ticket n°"]} {_ticket.Id}";
            else
                _header = Localize["Nuovo Ticket"];

            StateHasChanged();

        }

        private async Task LoadTicket()
        {
            if (IdTicket != null)
                _ticket = await _serviceTicket.Get((int)IdTicket);

            if (_ticket == null)
                _ticket = new Ticket();
        }
        private async Task<List<TicketSchedulerViewModel>> LoadTickets()
        {
            string backColor = "white";

            TicketFilter filter = new TicketFilter();
            if (IdUser != null && IdUser.Length > 0)
            {
                filter.IdUserAssigned = IdUser;
            }

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
            return _tickets;
        }

        protected async void OnChangeIdUser()
        {
            //StateHasChanged();
            await _schedulerTickets.Update();
        }

     
        private async Task LoadSettings()
        {
            var settings = await RestClientService.GetFirst<GlobalSetting>(ConstHelper.GlobalSettingsPath); //<GlobalSetting, int> _serviceSettings.Get();

            if (settings != null)
            {
                _maxNumMonthlyTicket = settings.MonthlySchedulerMaxNumTickets;
               
            }
        }

        private void HandleValidSubmit()
        {
            DialogService.CloseSide(new SchedulerUserDate() { Date = Date, IdUser = IdUser });

        }

        private void Cancel()
        {

            DialogService.CloseSide();
        }

      
    }
}
