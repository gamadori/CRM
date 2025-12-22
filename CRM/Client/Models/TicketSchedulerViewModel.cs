using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Models
{
    public class TicketSchedulerViewModel
    {
        public int Id { get; set; }

        public DateTime? Date { get; set; }

        public TimeOnly? Time { get; set; }

        public DateTime? DateEnd { get; set; }   
        public string User { get; set; }

        public string BackColor { get; set; }
        public string Company { get; set; }

        public string Description { get; set; }

        public int Status { get; set; }

        public bool Expired { get; set; }
    }
}
