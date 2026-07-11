using CRM.Client.Pages.Groups;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Tickets
{
    public partial class Close: ComponentBase
    {
        [Inject]
        NavigationManager NavigationManager { get; set; }


        [Inject]
        ITicketsService _service { get; set; }

        [Inject]
        IBaseRestService<ApplicationUser, UsersFilterModel, string> _usersService { get; set; }

        [Inject]
        IManyToManyService<UserGroupModel> _userGroupService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }


        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        TicketClose _ticketClose = new TicketClose();

        Ticket _ticket;

        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private bool _showNotes;

        /// <summary>True se il ticket ha un riepilogo operativo riutilizzabile come soluzione.</summary>
        private bool _hasOperationalSummary => !string.IsNullOrWhiteSpace(_ticket?.OperationalSummary);


        protected override void OnParametersSet()
        {
            //the param will be set now
            _ticketClose.Id = Id;


        }

        protected override async Task OnInitializedAsync()
        {
            _ticket = await _service.Get(Id);

            _ticketClose.Description = _ticket.CloseDescription;
            _ticketClose.Note = _ticket.CloseNote;
            _ticketClose.Support = _ticket.Support;

            // Apri automaticamente le note interne se ne esistono già
            _showNotes = !string.IsNullOrWhiteSpace(_ticketClose.Note);
        }

        private void ToggleNotes() => _showNotes = !_showNotes;

        /// <summary>Copia il riepilogo operativo del ticket nella descrizione di chiusura.</summary>
        private void UseOperationalSummary()
        {
            if (_hasOperationalSummary)
                _ticketClose.Description = _ticket.OperationalSummary;
        }

        /// <summary>Chiave testuale della priorità (low/medium/high) per lo stile del chip.</summary>
        private string GetPriorityKey() => ((TicketPriorities)_ticket.Priority) switch
        {
            TicketPriorities.Low => "low",
            TicketPriorities.Medium => "medium",
            TicketPriorities.High => "high",
            _ => "low"
        };

        /// <summary>Etichetta leggibile della priorità.</summary>
        private string GetPriorityLabel() => ((TicketPriorities)_ticket.Priority) switch
        {
            TicketPriorities.Low => "bassa",
            TicketPriorities.Medium => "media",
            TicketPriorities.High => "alta",
            _ => "bassa"
        };
        
        protected async Task HandleValidSubmit()
        {

            bool resp;

            try
            {
                if (await DialogService.Confirm(Localize["Chiudere il Ticket?"], Localize["Chiusura Ticket"]) == true)
                {
                    Ticket ticket = await _service.Get(Id);

                    if (ticket != null)
                    {


                        resp = await _service.CloseTicket(Id, _ticketClose);

                        if (resp)
                        {
                            if (OnClickSave != null)
                                OnClickSave();
                            else
                                NavigationManager.NavigateTo($"/Tickets/{Id}");
                        }
                    }
                }
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        protected void Cancel()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo($"/Tickets/{Id}");
        }
    }
}
