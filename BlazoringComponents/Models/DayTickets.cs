using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Models
{
    public class DayTickets
    {
        public DateTime Date { get; set; }

        public string NameDay { get; set; }

        public bool IsHoliday { get; set; }

        public string BgHead { get; set; }

        public string BgBody { get; set; }

        public bool IsMonthCurrent { get; set; }

        public string DescOthers { get; set; }
        
        public List<SchedulerTicket> Tickets { get; set; }
    }

   
}
