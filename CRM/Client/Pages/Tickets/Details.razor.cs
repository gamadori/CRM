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
    public partial class Details: ComponentBase
    {
        [Inject]
        private ITicketService _service { get; set; }

       
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public object IdTicket { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public Action OnClickTicketClose { get; set; }

        [Parameter]
        public EventCallback OnClickPrint { get; set; }

        [Parameter]
        public bool ViewCommands { get; set; } = true;

        [Parameter]
        public bool HeaderVisible { get; set; } = false;

        [Parameter]
        public string BackUrl { get; set; }


        private bool _panelAssign = false;

        private TicketModel _ticket = null;


       


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

                    _ticket = await _service.GetDetails(Id.Value);
                }
                else
                    _ticket = new TicketModel();


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            finally
            {
                await InvokeAsync(StateHasChanged);
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
                BackToUrl();
            //NavigationManager.NavigateTo("/Tickets/Index");
        }

        protected void TicketClose()
        {
            if (OnClickTicketClose != null)
                OnClickTicketClose();
            else
                NavigationManager.NavigateTo($"/Tickets/Close/{Id}");
        }

        private async void TicketPrint()
        {
            if (OnClickPrint.HasDelegate)
                await OnClickPrint.InvokeAsync();
            else
                NavigationManager.NavigateTo($"/Tickets/Report/{Id}");
        }
        private void PrepareAssign()
        {
            _panelAssign = true;
            StateHasChanged();
        }

        private async Task OnAssignClose()
        {
            _panelAssign = false;
            await LoadData();
            StateHasChanged();
        }
        protected void SendInvitation()
        {

        }

        private void BackToUrl()
        {
            if (BackUrl == null || BackUrl.Length == 0)
            {
                BackUrl = "/Tickets/Index";
            }
            else
                BackUrl = BackUrl.Replace("-", "/");

            NavigationManager.NavigateTo($"/Tickets/Index/{BackUrl}");
        }


    }
}
