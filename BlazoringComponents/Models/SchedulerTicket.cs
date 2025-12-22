using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Models
{
    public class SchedulerTicket
    {
        public string Id { get; set; }
        public DateTime DateStart { get; set; }

        public DateTime? TimeStart { get; set; }
       
        public DateTime DateEnd { get; set; }

        public string BackGroundColor { get; set; }

        public string StatusColor { get; set; }

        public string Company { get; set; }

        public string User { get; set; }

       public string Description { get; set; }


    }
}
