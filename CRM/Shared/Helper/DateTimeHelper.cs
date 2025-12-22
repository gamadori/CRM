using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QLNet;

namespace CRM.Shared.Helper
{
    public static class DateTimeHelper
    {
        public static DateTime AddBusinessDays(this DateTime date, int days)
        {
            Calendar c = new Italy(Italy.Market.Settlement);
            int numDays = 0;
            
            DateTime d = date;

            while (numDays < days)
            {
                if (c.isBusinessDay(d))
                {
                    numDays++;
                }
                d = d.AddDays(1);
            }
            return d;
        }

        public static int BusinessDaysBetween(DateTime from, DateTime to)
        {
            Calendar c = new Italy(Italy.Market.Settlement);

            return c.businessDaysBetween(from, to);
        }

        public static string MinuteFormat2(int minute)
        {
            TimeSpan time = TimeSpan.FromMinutes(minute);
            
            return time.ToString(@"hh\:mm");
        }

        public static string MinuteFormat(int? value)
        {
            if (value == null)
                return "";
            else
            {
                int m = value.Value % 60;
                int h = value.Value / 60;
                return $"{h.ToString("00")}:{m.ToString("00")}";
            }
        }
    }
}
