using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Tickets
{
    [Authorize]
    public partial class Preview: ComponentBase
    {
        [Inject]
        private ITicketService _service { get; set; }

        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> _serviceUsers { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public object IdTicket { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public bool ViewCommands { get; set; } = true;

        private Ticket _ticket = null;

        private ApplicationUser _userOpened = null;

        private ApplicationUser _userAssigned = null;

       

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
                    _userOpened = await _serviceUsers.Get(_ticket.IdUserOpened);
                    _userAssigned = await _serviceUsers.Get(_ticket.IdUserAssigned);
                }
                else
                    _ticket = new Ticket();


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        

        protected void Edit()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/Tickets/Edit/{Id}");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo("/Tickets/Index");
        }

        protected void SendInvitation()
        {

        }

    }
}
