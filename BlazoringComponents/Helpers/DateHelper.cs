using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Helpers
{
    public static class DateHelper
    {
        public static DateTime GetFirstDayOfWeek(this DateTime date)
        {
            int days = ((int)date.DayOfWeek - (int)DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek);
            if (days < 0)
                days = 7 + days;

            return date.AddDays(-days);
        }

        public static DateTime GetLastDayOfWeek(this DateTime date)
        {
            var d = date.GetFirstDayOfWeek();

            return date.AddDays(6);
        }
        public static DateTime GetFirstDayOfMonth(this DateTime date)
        {
            return new DateTime(date.Year, date.Month, 1);
        }

        public static DateTime GetLastDayOfMonth(this DateTime date)
        {
            return date.AddMonths(+1).AddDays(-date.Day);
        }

        public static DayWeek[] GetWeekDays()
        {
            List<DayWeek> names = new List<DayWeek>();
            var culture = CultureInfo.CurrentCulture;

            var day = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek; 
           
            for (int i = 0; i < 7; ++i)
            {
                var d = (int)day + i;

                if (d >= 7)
                    d = d - 7;

                names.Add(new DayWeek() { Name = culture.TextInfo.ToTitleCase(DateTimeFormatInfo.CurrentInfo.GetDayName((DayOfWeek)d)), IsHoliday = d == 0 || d == 6 });
            }
            return names.ToArray();
        }

    }

    public  class DayWeek
    {
        public  string Name { get; set; }

        public  bool IsHoliday { get; set; }
    }
}
