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
        /// <summary>Calendario festivo italiano. Immutabile: si costruisce una volta sola.</summary>
        private static readonly WorkingDayCultureInfo _culture = new("it-IT");

        /// <summary>Giorno feriale non festivo (lun-ven esclusi i festivi italiani).</summary>
        private static bool IsWorkday(DateTime date)
            => date.DayOfWeek != DayOfWeek.Saturday
               && date.DayOfWeek != DayOfWeek.Sunday
               && !date.IsHoliday(_culture);

        public static DateTime AddWorkdays(this DateTime originalDate, int workDays)
        {
            DateTime tmpDate = originalDate;
            while (workDays > 0)
            {
                tmpDate = tmpDate.AddDays(1);
                if (IsWorkday(tmpDate))
                    workDays--;
            }
            return tmpDate;
        }

        /// <summary>Sposta la data indietro di N giorni lavorativi (0 = data invariata).</summary>
        public static DateTime SubtractWorkdays(this DateTime originalDate, int workDays)
        {
            DateTime tmpDate = originalDate;
            while (workDays > 0)
            {
                tmpDate = tmpDate.AddDays(-1);
                if (IsWorkday(tmpDate))
                    workDays--;
            }
            return tmpDate;
        }

        /// <summary>Giorni lavorativi compresi tra le due date, estremi inclusi (0 se to &lt; from).</summary>
        public static int CountWorkdays(this DateTime from, DateTime to)
        {
            var start = from.Date;
            var end = to.Date;
            if (end < start)
                return 0;

            int count = 0;
            for (var d = start; d <= end; d = d.AddDays(1))
                if (IsWorkday(d))
                    count++;
            return count;
        }

        /// <summary>Primo giorno lavorativo &gt;= data. Serve a non far iniziare una fase di sabato.</summary>
        public static DateTime NextWorkday(this DateTime date)
        {
            var d = date.Date;
            while (!IsWorkday(d))
                d = d.AddDays(1);
            return d;
        }

        //public static bool IsHoliday(this DateTime originalDate)
        //{
        //    originalDate.IsHoliday()
        //    // INSERT YOUR HOlIDAY-CODE HERE!
        //    return DateSystem.IsPublicHoliday(originalDate, CountryCode.IT);
            
        //}
    }
}
