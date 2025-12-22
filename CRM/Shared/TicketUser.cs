using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    internal class TicketUser
    {
        [Key]
        public int Id { get; set; }

        public int IdTicket { get; set; }

        public string IdUser { get; set; }

        
    }
}
