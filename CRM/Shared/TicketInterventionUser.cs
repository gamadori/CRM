using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class TicketInterventionUser
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("TicketIntervention")]
        public int IdIntervention { get; set; }

        [ForeignKey("User")]
        public string IdUser { get; set; }

        public virtual TicketIntervention TicketIntervention { get; set; }

        public virtual ApplicationUser User { get; set; }
    }
}
