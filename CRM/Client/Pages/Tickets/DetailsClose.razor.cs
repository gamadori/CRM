using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Radzen;
using Microsoft.Extensions.Localization;

namespace CRM.Client.Pages.Tickets
{
    [Authorize]
    public partial class DetailsClose: ComponentBase
    {
        [Inject]
        ITicketsService _service { get; set; }

        [Inject]
        IBaseRestService<ApplicationUser, UsersFilterModel, string> _serviceUsers { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public object IdTicket { get; set; }

       
        [Parameter]
        public EventCallback OnClickTicketReOpen { get; set; }


       
        private Ticket _ticket = null;

       

        private ApplicationUser _userClosed = null;


        protected override async Task OnInitializedAsync()
        {
            if (Id == null && IdTicket != null && int.TryParse(IdTicket.ToString(), out int id))
            {
                Id = id;
            }
            await LoadData();
        }

        protected override async Task OnParametersSetAsync()
        {
            if (IdTicket != null && int.TryParse(IdTicket.ToString(), out int id))
            {
                Id = id;
                await LoadData();
            }
        }
        private async Task LoadData()
        {
            try
            {

                if (Id != null)
                {

                    _ticket = await _service.Get(Id.Value);
                    
                    if (_ticket.State?.State == (int)eTicketStates.Closed)
                    {
                        _userClosed = await _serviceUsers.Get(_ticket.IdUserClosed);
                    }
                }
                else
                    _ticket = new Ticket();


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        

        private async Task PrepareTicketReOpen()
        {
            if (await DialogService.Confirm(Localize["Riaprire il Ticket?"], Localize["Ticket"]) == true)
            {
                await TicketReOpen();
            }
        }

        private async Task TicketReOpen()
        {
            if (Id != null)
            {
                await _service.ReOpenTicket(Id.Value, _ticket);

                if (OnClickTicketReOpen.HasDelegate)
                    await OnClickTicketReOpen.InvokeAsync();

                
                //await LoadData();
               
            }
        }
       

    }
}
