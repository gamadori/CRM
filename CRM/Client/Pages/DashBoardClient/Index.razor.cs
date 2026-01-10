using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static CRM.Client.Program;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace CRM.Client.Pages.DashBoardClient
{
    [Authorize]
    public partial class Index : ComponentBase, INotificationHandler<MsgNotify>, IDisposable
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        //private IReportService<TicketDashBoardModel, TicketDashBoardModelFilter> _service { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

       

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        TicketDashBoardModel _tickets;
        Ticket _ticket;

        protected override async Task OnInitializedAsync()
        {
            DynamicNotificationHandlers.Register(this);

            await LoadData();

            await base.OnInitializedAsync();

            
        }

        public async Task Handle(MsgNotify notification, System.Threading.CancellationToken cancellationToken)
        {
            var id = notification.Id;
            var sender = notification.Sender;
            await LoadData();
            StateHasChanged();

        }

        private async Task LoadData()
        {

            _tickets = await RestClientService.GetFirst<TicketDashBoardModel>( ConstHelper.TicketsDashboardPath); //_service.Get(new TicketDashBoardModelFilter());
        }
        protected void AddTicket()
        {
            NavigationManager.NavigateTo("/Tickets/Create");
        }
        private void ComapanyDetails()
        {
            NavigationManager.NavigateTo("/Companies/Customer/Details");
        }

        private void TicketWorking()
        {
            NavigationManager.NavigateTo($"/Tickets/Index/{(int)TicketTypeSearch.Working}");

        }

        private void Tickets()
        {
            NavigationManager.NavigateTo($"/Tickets/Index");
        }

        private void Articles()
        {
            NavigationManager.NavigateTo($"/Articles");
        }

        private void TicketsNewMessage()
        {
            NavigationManager.NavigateTo($"/DashBoard/Tickets/{(int)TicketTypeSearch.NewMessage}");
        }

        

        public void Dispose()
        {

            DynamicNotificationHandlers.Unregister(this);
        }
    }
}
