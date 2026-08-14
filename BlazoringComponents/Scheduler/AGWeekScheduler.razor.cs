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
   
    public partial class AGWeekScheduler<TItem>: ComponentBase
    {
        [CascadingParameter(Name = "DateCurrent")]
        public DateTime DateCurrent { get; set; }


        [CascadingParameter(Name = "DateProperty")]
        public string DateProperty { get; set; }

        [CascadingParameter(Name = "TimeProperty")]
        public string TimeProperty { get; set; }

        [CascadingParameter(Name = "DateEndProperty")]
        public string DateEndProperty { get; set; }

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

        [CascadingParameter]
        public SchedulerDragDropContext DragDropContext { get; set; }

        [Parameter]
        public Action<string> OpenModal { get; set; }

        
        [Parameter]
        public EventCallback<DateTime> OnSelect { get; set; }

        [Parameter]
        public string ColorText { get; set; }

       

        public Action<DateTime, DateTime> ChangePeriod;

        private DateTime _dateStart;
        private DateTime _dateEnd;
       
        private List<DayTickets> _weekTickets = null;

        protected override void OnParametersSet()
        {

            if (Items == null)
                Items = new List<TItem>(); 
           
            _weekTickets = new List<DayTickets>();

            List<SchedulerTicket>  tickets = new List<SchedulerTicket>();
            foreach (var item in Items)
            {
                tickets.Add(SchedulerTicket.FromItem(
                    item,
                    DateProperty,
                    TimeProperty,
                    DateEndProperty,
                    UserProperty,
                    CompanyProperty,
                    DescriptionProperty,
                    BackColorProperty));
            }
            
            _dateStart = DateCurrent.Date.GetFirstDayOfWeek();
            _dateEnd = _dateStart.AddDays(6);

            var dateEnd = _dateEnd.AddDays(1);

            var weekTickets = tickets
                .Where(x => x.DateStart >= _dateStart && x.DateStart < dateEnd
                    || x.EffectiveDateEnd >= _dateStart && x.EffectiveDateEnd < dateEnd)
                .ToList();

            Calendar c = new Italy(Italy.Market.Settlement);

            

            for (var date = _dateStart; date <= _dateEnd; date = date.AddDays(1))
            {
                DateTime dateTo = date.Date.AddDays(1);

                DayTickets day = new DayTickets();
                day.Date = date.Date;
                day.NameDay = date.GetDayName();
                day.IsHoliday = c.isHoliday(date);
                day.Tickets = weekTickets
                    .Where(x => day.Date >= x.DateStart.Date && day.Date <= x.EffectiveDateEnd.Date
                        || x.DateStart.Date == day.Date)
                    .OrderBy(x => x.IsScheduled)
                    .ThenBy(x => x.TimeStart)
                    .ToList();
              //  day.Tickets = weekTickets.Where(x => x.DateStart >= day.Date && x.DateStart < dateTo || x.DateStart >= x.DateEnd && x.DateEnd < dateTo).OrderBy(x => x.TimeStart).ToList();
                day.IsMonthCurrent = true;
                day.BgHead = DayHelper.GetBgHeader(day.IsHoliday, true, day.Date.Date == DateCurrent.Date);
                day.BgBody = DayHelper.GetBgBody(day.IsHoliday);
                 _weekTickets.Add(day);
            }
            base.OnParametersSet();
        }

        public void Init()
        {
            
        }

        private void CreateDayOfWeek()
        {
            _dateStart = DateCurrent.GetFirstDayOfWeek();
            _dateEnd = _dateEnd.AddDays(6);

            for (var d = _dateStart; d <= _dateEnd; d = d.AddDays(1))
            {

            }
        }
    }
}
