using BlazoringComponents.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System;
using System.Threading.Tasks;

namespace BlazoringComponents.Scheduler
{
    public partial class AGDayHead: ComponentBase
    {
        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

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
