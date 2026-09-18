using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Helpers
{
    public static class DayHelper
    {
        /// <summary>
        /// Classe dell'intestazione del giorno. Prima erano blocchi Bootstrap pieni
        /// (bg-primary, bg-danger, bg-success): un calendario tutto blu e rosso, e in tema
        /// scuro illeggibile. Ora sono classi proprie, vestite in AGDayHead con i colori
        /// del tema (--crm-*): il tipo di giorno si legge dal colore del testo e da un filo.
        /// </summary>
        public static string GetBgHeader(bool isHoliday, bool isMonth = true, bool currentDate = false)
        {
            if (currentDate)
            {
                return "day-head-today";
            }
            else if (!isMonth)
            {
                return "day-head-other";
            }
            else if (isHoliday)
            {
                return "day-head-holiday";
            }
            else
                return "day-head-work";
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
