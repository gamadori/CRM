using CRM.Client.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components
{
    public partial class BtnScheduler: ComponentBase
    {
        [Inject]
        DialogService DialogService { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public EventCallback<SchedulerUserDate> OnGetItem { get; set; }

      
        [Parameter]
        public DateTime Date { get; set; } = DateTime.Today;

        [Parameter]
        public string? IdUser { get; set; }

        [Parameter]
        public int? IdTicket { get; set; }

        [Parameter]
        public int? IdTicketType { get; set; }

        private async Task OpenScheduler()
        {
            var dateUser = await DialogService.OpenSideAsync<Pages.Tickets.TicketCalendar>(Localize["Seleziona Data..."], new Dictionary<string, object>() { { "Date", Date }, {  "IdUser", IdUser } , 
                    { "IdTicket", IdTicket }, {"IdTicketType", IdTicketType } } ,
                new SideDialogOptions { Position = DialogPosition.Top, ShowMask = false, Height = "auto", Style = "max-height: 90%;" });

            if (OnGetItem.HasDelegate)
                await OnGetItem.InvokeAsync(dateUser);

        }


        private void OnClickCancel()
        {
            DialogService.CloseSide();
        }

    }
}
