using AGUtility.Extensions;
using BlazoringComponents.Helpers;
using BlazoringComponents.Models;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using QLNet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Scheduler
{
    public partial class AGMonthScheduler<TItem> : ComponentBase
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

        [CascadingParameter(Name = "DateLocked")]
        public bool DateLocked { get; set; } = false;

        [Parameter]
        public Action<string> OpenModal { get; set; }

        [Parameter]
        public Func<DateTime, Task> OpenDaylyScheduler { get; set; }

        [Parameter]
        public EventCallback<DateTime> OnSelect { get; set; }

       

        [Parameter]
        public string ColorText { get; set; }


        [CascadingParameter(Name = "MaxNumDaylyTicket")]
        public int MaxNumDaylyTicket { get; set; }
 
        public Action<DateTime, DateTime> ChangePeriod;

        private DateTime _dateStart;
        private DateTime _dateEnd;

        private List<List<DayTickets>> _monthTickets = null;

        private DayWeek[] _weekDays;

        protected override void OnInitialized()
        {
            
            base.OnInitialized();
        }
        protected override void OnParametersSet()
        {

            if (Items == null)
                Items = new List<TItem>();

            GetWeekDays();

            _monthTickets = new List<List<DayTickets>>();

            List<SchedulerTicket> tickets = new List<SchedulerTicket>();
            foreach (var item in Items)
            {
                var model = new SchedulerTicket();
                model.Id = item.GetPropertyValue<object>("Id").ToString();
                model.DateStart = item.GetPropertyValue<DateTime>(DateProperty).Date;
                model.TimeStart = item.GetPropertyValue<DateTime>(TimeProperty);
                model.DateEnd = item.GetPropertyValue<DateTime>(DateEndProperty).Date;
                model.User = item.GetPropertyValue<string>(UserProperty);
                model.Company = item.GetPropertyValue<string>(CompanyProperty);
                model.Description = item.GetPropertyValue<string>(DescriptionProperty);
                model.BackGroundColor = item.GetPropertyValue<string>(BackColorProperty);
                tickets.Add(model);
            }

            _dateStart = DateCurrent.Date.GetFirstDayOfMonth();
            _dateEnd = _dateStart.GetLastDayOfMonth();

            var dateEnd = _dateEnd.AddDays(1);


            var c = new Italy(Italy.Market.Settlement);

            var dateStart = _dateStart.GetFirstDayOfWeek();
            dateEnd = _dateEnd.GetLastDayOfWeek();

            var monthTickets = tickets.Where(x => x.DateStart >= dateStart && x.DateStart < dateEnd || x.DateEnd >= dateStart && x.DateEnd < dateEnd).ToList();


            List<DayTickets> week = new List<DayTickets>();

            int nday = 0;
            for (var date = dateStart; date <= dateEnd; date = date.AddDays(1))
            {
                DateTime dateTo = date.Date.AddDays(1);

                DayTickets day = new DayTickets();
                day.Date = date.Date;
                day.NameDay = date.GetDayName();
                day.IsHoliday = c.isHoliday(date); //holidays.Contains(date);
                day.Tickets = monthTickets.Where(x => day.Date >= x.DateStart.Date && day.Date <= x.DateEnd || x.DateStart.Date == day.Date).OrderBy(x => x.TimeStart).ToList();

                if (day.Tickets.Count > MaxNumDaylyTicket)
                {
                    if (MaxNumDaylyTicket > 0)
                        day.DescOthers = "Altri Tickets: ";
                    else
                        day.DescOthers = "Totali Tickets: ";
                    day.DescOthers += $"{day.Tickets.Count - MaxNumDaylyTicket}";
                }
                day.IsMonthCurrent =  date.Month == DateCurrent.Month;

                day.BgHead = DayHelper.GetBgHeader(day.IsHoliday, day.IsMonthCurrent, day.Date.Date == DateCurrent.Date);
                day.BgBody = DayHelper.GetBgBody(day.IsHoliday, day.IsMonthCurrent);

                week.Add(day);

                if (++nday >= 7)
                {
                    nday = 0;
                    _monthTickets.Add(week);
                    week = new List<DayTickets>();
                }
            }
            base.OnParametersSet();

        }

        private void GetWeekDays()
        {
            
            //var dateTimeInfo = CultureInfo.CurrentCulture.DateTimeFormat;
            //_weekDays = dateTimeInfo.DayNames;
            _weekDays = DateHelper.GetWeekDays();
        }

       

    }
}
