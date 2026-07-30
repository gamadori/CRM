using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.DashBoard
{
    public partial class Tickets: ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int TypeSearch { get; set; }

        [Parameter]
        public string? IdUser { get; set; } = null;

        private string _header;

        protected override void OnParametersSet()
        {
            //the param will be set now
            switch ((TicketTypeSearch)TypeSearch)
            {
                case TicketTypeSearch.All:
                    _header = "Tickets";
                    break;

                case TicketTypeSearch.Assigned:
                    _header = "Tickets in Lavorazione";
                    break;

                case TicketTypeSearch.NotAssigned:
                    _header = "Ticket non Assegnati";
                    
                    break;

                case TicketTypeSearch.Expired:
                    _header = "Tickect Scaduti";
                    break;

                case TicketTypeSearch.Blocked:
                    _header = "Ticket bloccati";
                    break;

                case TicketTypeSearch.ToClaim:
                    _header = "Ticket da prendere in carico";
                    break;

            }
        }
        protected void AddTicket()
        {
            NavigationManager.NavigateTo("/Tickets/Create");
        }

        protected void Home()
        {
            NavigationManager.NavigateTo("/");
        }

        protected void Schedule()
        {
            NavigationManager.NavigateTo("/Tickets/Schedule");
        }
    }
}
