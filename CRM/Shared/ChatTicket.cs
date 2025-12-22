using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ChatTicket
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Ticket")]
        public int IdTicket { get; set; }

        public string IdUser { get; set; }

        public DateTime Date { get; set; }

        public bool Customer { get; set; }

        public string Message { get; set; }
        
        public bool Read { get; set; }

        public virtual Ticket Ticket { get; set; }
    }
}
