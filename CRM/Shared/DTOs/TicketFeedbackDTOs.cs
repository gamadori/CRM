using System;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared.DTOs
{
    /// <summary>
    /// DTO per la creazione di un feedback
    /// </summary>
    public class TicketFeedbackRequest
    {
        [Required]
        public int IdTicket { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }

    /// <summary>
    /// DTO per la visualizzazione dei ticket in attesa di feedback
    /// </summary>
    public class TicketPendingFeedback
    {
        public int TicketId { get; set; }
        public string Description { get; set; }
        public DateTime? DateClosed { get; set; }
        public string? CloseDescription { get; set; }
        public string Company { get; set; }
    }

    /// <summary>
    /// DTO per la risposta del feedback
    /// </summary>
    public class TicketFeedbackResponse
    {
        public int Id { get; set; }
        public int IdTicket { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UserName { get; set; }
    }
}
