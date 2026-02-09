using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>
    /// Feedback/Rating dato dal cliente dopo la chiusura di un ticket
    /// </summary>
    public class TicketFeedback
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID del ticket a cui si riferisce il feedback
        /// </summary>
        [Required]
        [ForeignKey("Ticket")]
        public int IdTicket { get; set; }

        /// <summary>
        /// Rating da 1 a 5 stelle
        /// </summary>
        [Required]
        [Range(1, 5, ErrorMessage = "Il rating deve essere compreso tra 1 e 5")]
        [Display(Name = "Valutazione")]
        public int Rating { get; set; }

        /// <summary>
        /// Commento opzionale del cliente
        /// </summary>
        [MaxLength(1000)]
        [Display(Name = "Commento")]
        public string? Comment { get; set; }

        /// <summary>
        /// ID dell'utente che ha lasciato il feedback
        /// </summary>
        [Required]
        public string IdUser { get; set; }

        /// <summary>
        /// Data e ora in cui è stato lasciato il feedback
        /// </summary>
        [Display(Name = "Data Feedback")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Indica se il feedback è stato letto/visualizzato dall'admin
        /// </summary>
        public bool IsRead { get; set; } = false;

        // Navigation properties
        public virtual Ticket Ticket { get; set; }
        public virtual ApplicationUser User { get; set; }
    }
}
