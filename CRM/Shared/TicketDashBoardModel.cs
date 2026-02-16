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

        public int TicketsClosed { get; set; }

        public int TicketsExpired { get; set; }

        public int UsersNeedConfirm { get; set; }

        public int ChatMessageToRead { get; set; }

        public int InterventionsPendingSignature { get; set; }

        /// <summary>
        /// Numero di feedback non letti dai clienti
        /// </summary>
        public int UnreadFeedbacksCount { get; set; }

        /// <summary>
        /// Lista dei feedback recenti non letti
        /// </summary>
        public List<FeedbackSummary> RecentFeedbacks { get; set; } = new();

        public List<Ticket> Tickets { get; set; }   
    }

    /// <summary>
    /// Riepilogo feedback per la dashboard
    /// </summary>
    public class FeedbackSummary
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public string TicketDescription { get; set; }
        public string CompanyName { get; set; }
        public string UserName { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }

    public class TicketDashBoardModelFilter
    {
        public string? IdUser { get; set; }
    }
}
