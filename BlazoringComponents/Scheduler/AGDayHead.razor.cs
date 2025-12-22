using BlazoringComponents.Models;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BlazoringComponents.Scheduler
{
    public partial class AGDayHead: ComponentBase
    {
        [Parameter]
        public DayTickets Day { get; set; }

        [CascadingParameter(Name = "CurrentView")]
        public SchedulerViews CurrentView { get; set; }

        [CascadingParameter(Name = "ViewMode")]
        public SchedulerViewMode ViewMode { get; set; } = SchedulerViewMode.Sheduler;

        [CascadingParameter(Name = "OnNewTicket")]
        public EventCallback<DateTime> OnNewTicket { get; set; }


        [Parameter]
        public EventCallback<DateTime> OnSelect { get; set; }

        

        private string _color = "color: white;";
        protected override void OnInitialized()
        {
            if (!Day.IsMonthCurrent)
            {
                _color = "color: gray;";
            }
            base.OnInitialized();
        }

        private async Task OnSelectDate(DateTime date)
        {
            if (OnSelect.HasDelegate)
                await OnSelect.InvokeAsync(date);
        }

        private async Task OnClickNewTicket(DateTime date)
        {
            if (OnNewTicket.HasDelegate)
            {
                await OnNewTicket.InvokeAsync(date);
            }
        }
    }
}
