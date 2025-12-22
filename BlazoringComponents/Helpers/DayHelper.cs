using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Helpers
{
    public static class DayHelper
    {
        public static string GetBgHeader(bool isHoliday, bool isMonth = true, bool currentDate = false)
        {
            if (currentDate)
            {
                return "bg-success bg-gradient text-white";
            }
            else if (!isMonth)
            {
                return "bg-black-50 bg-gradient text-secondary";
            }
            else if (isHoliday)
            {
                return "bg-danger bg-gradient text-white";
            }
            else
                return "bg-primary bg-gradient text-light";
        }

        public static string GetBgBody(bool isHoliday, bool isMonth = true)
        {
            if (!isMonth)
            {
                return "bg-light bg-gradient text-secondary";
            }
            else if (isHoliday)
            {
                return "DayHoliday";  //holiday";
            }
            else
                return "DayWork"; // "weekdays bg-gradient";
        }
    }
}
