using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGUtility.Extensions
{
    public static class DateTimeExtensions
    {
        public static string GetDayName(this DateTime date)
        {
            string _ret = string.Empty; //Only for .NET Framework 4++
            var culture = CultureInfo.CurrentCulture; //<- 'es-419' = Spanish (Latin America), 'en-US' = English (United States)
            _ret = culture.DateTimeFormat.GetDayName(date.DayOfWeek); //<- Get the Name     
            _ret = culture.TextInfo.ToTitleCase(_ret.ToLower()); //<- Convert to Capital title
            return _ret;
        }

        public static string GetMonthName(this DateTime date)
        {
            string _ret = string.Empty; //Only for .NET Framework 4++
            var culture = CultureInfo.CurrentCulture; //<- 'es-419' = Spanish (Latin America), 'en-US' = English (United States)
            _ret = culture.DateTimeFormat.GetMonthName(date.Month); //<- Get the Name     
            _ret = culture.TextInfo.ToTitleCase(_ret.ToLower()); //<- Convert to Capital title
            return _ret;
        }
    }
}
