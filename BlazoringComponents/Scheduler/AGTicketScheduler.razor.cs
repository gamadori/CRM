using BlazoringComponents.Models;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Scheduler
{
    public partial class AGTicketScheduler : ComponentBase
    {
        [Inject]
        private TooltipService tooltipService { get; set; }

        [CascadingParameter]
        public SchedulerTicket Ticket { get; set; }

        [Parameter]
        public Action<string> OpenModal { get; set; }

        

        void ShowTooltip(ElementReference elementReference, string txt, TooltipOptions options = null) => tooltipService.Open(elementReference, txt, options);
        void CloseTooltip() => tooltipService.Close();
        private string _styleTicket;
        protected override void OnInitialized()
        {

            base.OnInitialized();

        }
        protected override void OnParametersSet()
        {
            _styleTicket = $"background-color: {Ticket.BackGroundColor}; border-color: #313131; cursor: pointer";

            if (Ticket.User == null || Ticket.User.Length == 0)
                Ticket.User = "Non Assegnato";

            base.OnInitialized();
        }
    }
}
