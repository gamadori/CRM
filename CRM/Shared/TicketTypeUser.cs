using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class TicketTypeUser
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Tipo Ticket")]
        [Required]
        public int IdTicket { get; set; }

        [Display(Name = "Utente")]
        [Required]
        public string IdUser { get; set; }


    }
}
