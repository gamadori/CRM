using DateTimeExtensions;
using DateTimeExtensions.WorkingDays;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Extensions
{
    public static class DateTimeExtensions
    {
        public static DateTime AddWorkdays(this DateTime originalDate, int workDays)
        {
            var culture = new WorkingDayCultureInfo("it-IT");

            DateTime tmpDate = originalDate;
            while (workDays > 0)
            {
                tmpDate = tmpDate.AddDays(1);
                if (tmpDate.DayOfWeek < DayOfWeek.Saturday &&
                    tmpDate.DayOfWeek > DayOfWeek.Sunday &&
                    !tmpDate.IsHoliday(culture))
                    workDays--;
            }
            return tmpDate;
        }

        //public static bool IsHoliday(this DateTime originalDate)
        //{
        //    originalDate.IsHoliday()
        //    // INSERT YOUR HOlIDAY-CODE HERE!
        //    return DateSystem.IsPublicHoliday(originalDate, CountryCode.IT);
            
        //}
    }
}
