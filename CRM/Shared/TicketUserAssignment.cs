using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>
    /// Rappresenta l'assegnazione di un utente a un ticket (relazione many-to-many)
    /// </summary>
    [Table("TicketUserAssignments")]
    public class TicketUserAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdTicket { get; set; }

        [Required]
        public string IdUser { get; set; }

        /// <summary>
        /// Data e ora in cui l'utente è stato assegnato al ticket
        /// </summary>
        public DateTime AssignedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Utente che ha effettuato l'assegnazione
        /// </summary>
        public string? AssignedBy { get; set; }

        // Navigation properties
        public virtual Ticket Ticket { get; set; }
        public virtual ApplicationUser User { get; set; }
    }
}
