using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class TicketInterventionType
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("InterventionType")]
        public int IdInterventionType { get; set; }

        [ForeignKey("TicketIntervention")]
        public int IdTicketIntervention { get; set; }

        public TicketIntervention TicketIntervention { get; set; }

        public InterventionType InterventionType { get; set; }
    }

    public class IterventionTypeReport
    {
        public int Id { get; set; }

        public string Desc { get; set; }

        public bool Checked { get; set; }
    }

}
