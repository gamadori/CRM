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
    /// <summary>
    /// Tipologia di tempo registrato nell'intervento
    /// </summary>
    public enum InterventionTimeType
    {
        /// <summary>
        /// Tempo di lavoro effettivo presso il cliente
        /// </summary>
        [Display(Name = "Lavoro")]
        Work = 0,

        /// <summary>
        /// Tempo di viaggio (andata/ritorno)
        /// </summary>
        [Display(Name = "Viaggio")]
        Travel = 1,

        /// <summary>
        /// Pausa (non fatturabile)
        /// </summary>
        [Display(Name = "Pausa")]
        Break = 2
    }
    
    public class TicketInterventionTime
    {
        public int Id { get; set; }

        [ForeignKey(nameof(TicketIntervention))]
        public int IdTicketIntervention { get; set; }

        [Display(Name = "Data e Orario di Inizio")]
        public DateTime StartDateTime { get; set; }

        [Display(Name = "Orario di Fine")]
        public DateTime EndDateTime { get; set; }

        /// <summary>
        /// Tipo di tempo: Lavoro, Viaggio, Pausa
        /// </summary>
        [Display(Name = "Tipo")]
        public InterventionTimeType TimeType { get; set; } = InterventionTimeType.Work;

        /// <summary>
        /// Note aggiuntive (es. "Viaggio A/R da Milano", "Pausa pranzo")
        /// </summary>
        [Display(Name = "Note")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>
        /// Se true, questo periodo è fatturabile al cliente
        /// </summary>
        [Display(Name = "Fatturabile")]
        public bool IsBillable { get; set; } = true;

        /// <summary>
        /// Chilometri percorsi (se TimeType = Travel)
        /// </summary>
        [Display(Name = "Km Percorsi")]
        public int? TravelKilometers { get; set; }

        /// <summary>
        /// Durata calcolata in minuti (proprietà computed)
        /// </summary>
        [NotMapped]
        public int DurationMinutes => (int)(EndDateTime - StartDateTime).TotalMinutes;

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

        [Display(Name = "Tipo")]
        public InterventionTimeType TimeType { get; set; } = InterventionTimeType.Work;

        [Display(Name = "Note")]
        public string? Notes { get; set; }

        [Display(Name = "Fatturabile")]
        public bool IsBillable { get; set; } = true;

        [Display(Name = "Km Percorsi")]
        public int? TravelKilometers { get; set; }

        [NotMapped]
        public int DurationMinutes => (int)(EndDateTime - StartDateTime).TotalMinutes;

        [NotMapped]
        public string DurationFormatted => $"{DurationMinutes / 60}h {DurationMinutes % 60}m";

        [NotMapped]
        public string TimeTypeDescription => TimeType switch
        {
            InterventionTimeType.Work => "Lavoro",
            InterventionTimeType.Travel => "Viaggio",
            InterventionTimeType.Break => "Pausa",
            _ => "Altro"
        };

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

        public InterventionTimeType? TimeType { get; set; }

        public bool? IsBillable { get; set; }
    }

    public static class TicketIntervemtionTimeHelper
    {
        public static TicketInterventionTimeModel ToModel(this TicketInterventionTime time)
        {
            return new TicketInterventionTimeModel() 
            { 
                Id = time.Id, 
                StartDateTime = time.StartDateTime, 
                EndDateTime = time.EndDateTime, 
                IdTicketIntervention = time.IdTicketIntervention,
                TimeType = time.TimeType,
                Notes = time.Notes,
                IsBillable = time.IsBillable,
                TravelKilometers = time.TravelKilometers
            };
        }

        /// <summary>
        /// Calcola il totale dei minuti di lavoro (escludendo pause)
        /// </summary>
        public static int TotalWorkMinutes(this IEnumerable<TicketInterventionTime> times)
        {
            return times
                .Where(t => t.TimeType == InterventionTimeType.Work)
                .Sum(t => t.DurationMinutes);
        }

        /// <summary>
        /// Calcola il totale dei minuti di viaggio
        /// </summary>
        public static int TotalTravelMinutes(this IEnumerable<TicketInterventionTime> times)
        {
            return times
                .Where(t => t.TimeType == InterventionTimeType.Travel)
                .Sum(t => t.DurationMinutes);
        }

        /// <summary>
        /// Calcola il totale dei minuti fatturabili
        /// </summary>
        public static int TotalBillableMinutes(this IEnumerable<TicketInterventionTime> times)
        {
            return times
                .Where(t => t.IsBillable)
                .Sum(t => t.DurationMinutes);
        }

        /// <summary>
        /// Calcola il totale dei chilometri percorsi
        /// </summary>
        public static int TotalTravelKilometers(this IEnumerable<TicketInterventionTime> times)
        {
            return times
                .Where(t => t.TimeType == InterventionTimeType.Travel && t.TravelKilometers.HasValue)
                .Sum(t => t.TravelKilometers!.Value);
        }
    }

}
