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

        /// <summary>Ticket senza utente E senza gruppo: lavoro ancora da smistare.</summary>
        public int TicketsNotAssigned { get; set; }

        /// <summary>
        /// Ticket smistati a un gruppo ma non ancora presi in carico da nessuno. Non sono
        /// "da assegnare": hanno gia' un destinatario, gli manca il responsabile.
        /// </summary>
        public int TicketsToClaim { get; set; }

        public int TicketsAssigned { get; set; }

        public int TicketAssigned { get; set; }

        public int TicketsClosed { get; set; }

        public int TicketsExpired { get; set; }

        public int UsersNeedConfirm { get; set; }

        public int ChatMessageToRead { get; set; }

        public int BlockedTickets { get; set; }

        public int LateExpectedCommesse { get; set; }

        /// <summary>Email in ingresso non ancora prese in carico da un operatore.</summary>
        public int InboundEmailsToHandle { get; set; }

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
