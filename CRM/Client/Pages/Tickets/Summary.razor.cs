using BlazoringComponents.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Tickets
{
    public partial class Summary: ComponentBase
    {
        [Inject]
        ITicketService TicketService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }


        [Parameter]
        public int Id { get; set; }

        private TicketModel _ticket = null;


        protected override async Task OnInitializedAsync()
        {
            await LoadData();
            await base.OnInitializedAsync();
        }


        private async Task LoadData()
        {
            _ticket = await TicketService.GetDetails(Id);
        }


        private void OpenTicket()
        {
            DialogService.CloseSide();
            NavigationManager.NavigateTo($"/Tickets/{Id}");
        }
       
    }
}
