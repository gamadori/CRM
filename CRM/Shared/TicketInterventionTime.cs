using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    
    public class TicketInterventionTime
    {
        public int Id { get; set; }

        [ForeignKey(nameof(TicketIntervention))]
        public int IdTicketIntervention { get; set; }

        [Display(Name = "Data e Orario di Inizio")]
        public DateTime StartDateTime { get; set; }

        [Display(Name = "Orario di Fine")]
        public DateTime EndDateTime { get; set; }

        public virtual TicketIntervention TicketIntervention { get; set; }

        
    }

    public class TicketInterventionTimeModel
    {
        public int Id { get; set; }

        public int IdTicketIntervention { get; set; }

        [Display(Name = "Data e Orario di Inizio")]
        public DateTime StartDateTime { get; set; }

        [Display(Name = "Orario di Fine")]
        public DateTime EndDateTime { get; set; }

        public int Permits;
        
    }

    public class TicketInterventionTimeFilter
    {
        public int Id { get; set; }

        public int IdTicketIntervention { get; set; }

        [Display(Name = "Data e Orario di Inizio")]
        public DateTime StartDateTime { get; set; }

        [Display(Name = "Orario di Fine")]
        public DateTime EndDateTime { get; set; }


    }

    public static class TicketIntervemtionTimeHelper
    {
        public static TicketInterventionTimeModel ToModel(this TicketInterventionTime time)
        {
            return new TicketInterventionTimeModel() { Id = time.Id, StartDateTime = time.StartDateTime, EndDateTime = time.EndDateTime, IdTicketIntervention = time.IdTicketIntervention };
        }
    }

}
