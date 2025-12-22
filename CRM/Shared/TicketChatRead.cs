using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class TicketChatRead
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("TicketChat")]
        public int IdTicketChat { get; set; }
        
        [ForeignKey("User")]
        public string? IdUser { get; set; }

        public bool Displayed { get; set; }

        public DateTime DateRead { get; set; }

        public virtual ApplicationUser? User { get; set; }

        public virtual TicketChat TicketChat { get; set; }
    }
}
