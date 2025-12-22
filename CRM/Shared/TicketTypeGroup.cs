using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class TicketTypeGroup
    {
        public int Id { get; set; }

        [Required]
        [ForeignKey("Ticket")]
        public int IdTicket { get; set; }

        [Required]
        [ForeignKey("Group")]
        public int IdGroup { get; set; }

        public bool CanOpen { get; set; }

        public bool CanAssigned { get; set; }

        public virtual Ticket Ticket { get; set; }
        
        public virtual Group Group { get; set; }
    }
}
