using BlazoringComponents.Scheduler;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Tickets
{
    public partial class TicketPlanningPicker : ComponentBase
    {
        [Inject]
        ITicketsService _serviceTicket { get; set; }

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

        private Ticket _ticket = null;

        private string _header;

        protected override async Task OnInitializedAsync()
        {
            await LoadTicket();

            _header = _ticket.Id > 0
                ? $"{Localize["Ticket n."]} {_ticket.Id}"
                : Localize["Nuovo Ticket"];
        }

        private async Task LoadTicket()
        {
            if (IdTicket != null)
                _ticket = await _serviceTicket.Get((int)IdTicket);

            _ticket ??= new Ticket();
        }

        private void HandleValidSubmit()
        {
            DialogService.CloseSide(new TicketPlanningSelection { Date = Date, IdUser = IdUser });
        }

        private void Cancel()
        {
            DialogService.CloseSide();
        }
    }
}
