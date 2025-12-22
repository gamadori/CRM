using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class TicketDashBoardModel
    {
        public bool IsClient { get; set; }

        public int TicketsNotAssigned { get; set; }

        public int TicketsWorking { get; set; }

        public int TicketAssigned { get; set; }

        public int TicketsExpired { get; set; }

        public int UsersNeedConfirm { get; set; }

        public int ChatMessageToRead { get; set; }

        public List<Ticket> Tickets { get; set; }   


    }

    public class TicketDashBoardModelFilter
    {
        public string? IdUser { get; set; }
    }
}
