using AGUtility.Extensions;
using BlazoringComponents.Helpers;
using BlazoringComponents.Models;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using QLNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Scheduler
{
    public partial class AGDayScheduler<TItem>: ComponentBase
    {
        [CascadingParameter(Name = "DateCurrent")]
        public DateTime DateCurrent { get; set; }


        [CascadingParameter(Name = "DateProperty")]
        public string DateProperty { get; set; }

        [CascadingParameter(Name = "DateEndProperty")]
        public string DateEndProperty { get; set; }

        [CascadingParameter(Name = "TimeProperty")]
        public string TimeProperty { get; set; }

        [CascadingParameter(Name = "UserProperty")]
        public string UserProperty { get; set; }

        [CascadingParameter(Name = "CompanyProperty")]
        public string CompanyProperty { get; set; }

        [CascadingParameter(Name = "DescriptionProperty")]
        public string DescriptionProperty { get; set; }

        [CascadingParameter(Name = "BackColorProperty")]
        public string BackColorProperty { get; set; }

        [CascadingParameter(Name = "Tickets")]
        public IEnumerable<TItem> Items { get; set; }

        [Parameter]
        public Action<string> OpenModal { get; set; }

        [Parameter]
        public EventCallback<DateTime> OnSelect { get; set; }

        [Parameter]
        public string ColorText { get; set; }

        private DayTickets _dayTickets;

        protected override void OnParametersSet()
        {

            if (Items == null)
                Items = new List<TItem>();

            _dayTickets = new DayTickets();

            List<SchedulerTicket> tickets = new List<SchedulerTicket>();

            foreach (var item in Items)
            {
                var model = new SchedulerTicket();

                model.Id = item.GetPropertyValue<object>("Id").ToString();
                model.DateStart = item.GetPropertyValue<DateTime>(DateProperty).Date;
                model.TimeStart = item.GetPropertyValue<DateTime>(TimeProperty);
                model.DateEnd = item.GetPropertyValue<DateTime>(DateEndProperty);
                model.User = item.GetPropertyValue<string>(UserProperty);
                model.Company = item.GetPropertyValue<string>(CompanyProperty);
                model.Description = item.GetPropertyValue<string>(DescriptionProperty);
                model.BackGroundColor = item.GetPropertyValue<string>(BackColorProperty);
                
                // ✅ REFACTORED: Usa GetPropertyValueSafe per gestione sicura reflection
                model.AssignedUserNames = item.GetPropertyValueSafe<List<string>>("AssignedUserNames", new List<string>());
                
                tickets.Add(model);
            }

            Calendar c = new Italy(Italy.Market.Settlement);

            _dayTickets.Date = DateCurrent.Date;
            _dayTickets.NameDay = _dayTickets.Date.GetDayName();
            _dayTickets.IsHoliday = c.isHoliday(DateCurrent.Date);
           // _dayTickets.Tickets = tickets.Where(x => x.DateStart >= DateCurrent.Date && x.DateStart < DateCurrent.Date.AddDays(1)).OrderBy(x=>x.TimeStart).ToList();
            _dayTickets.Tickets = tickets.Where(x => DateCurrent.Date >= x.DateStart.Date && DateCurrent.Date <= x.DateEnd || x.DateStart.Date == DateCurrent.Date).OrderBy(x => x.TimeStart).ToList();
            _dayTickets.BgHead = DayHelper.GetBgHeader(_dayTickets.IsHoliday, true, true);
            _dayTickets.BgBody = DayHelper.GetBgBody(_dayTickets.IsHoliday);
            _dayTickets.IsMonthCurrent = true;
            base.OnParametersSet();
        }
    }
}
