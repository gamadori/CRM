using System;
using System.Collections.Generic;

namespace CRM.Shared.Models
{
    /// <summary>
    /// DTO per rappresentare il carico di lavoro (workload) di un utente in una data specifica
    /// </summary>
    public class UserWorkloadInfo
    {
        /// <summary>
        /// ID dell'utente
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Nome completo dell'utente
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// Numero totale di ticket assegnati in quella giornata
        /// </summary>
        public int TicketCount { get; set; }

        /// <summary>
        /// Lista dei ticket assegnati (con dettagli minimi)
        /// </summary>
        public List<TicketWorkloadItem> Tickets { get; set; } = new();

        /// <summary>
        /// Livello di carico calcolato automaticamente
        /// </summary>
        public WorkloadLevel Level => TicketCount switch
        {
            0 => WorkloadLevel.Free,
            >= 1 and <= 2 => WorkloadLevel.Low,
            >= 3 and <= 5 => WorkloadLevel.Medium,
            _ => WorkloadLevel.High
        };

        /// <summary>
        /// Classe CSS per il badge (verde/giallo/rosso)
        /// </summary>
        public string BadgeClass => Level switch
        {
            WorkloadLevel.Free => "workload-free",
            WorkloadLevel.Low => "workload-low",
            WorkloadLevel.Medium => "workload-medium",
            WorkloadLevel.High => "workload-high",
            _ => "workload-free"
        };

        /// <summary>
        /// Testo da mostrare nel badge
        /// </summary>
        public string BadgeText => TicketCount switch
        {
            0 => "Libero",
            1 => "1 ticket",
            _ => $"{TicketCount} tickets"
        };

        /// <summary>
        /// Emoji da mostrare accanto al badge
        /// </summary>
        public string Icon => Level switch
        {
            WorkloadLevel.Free => "?",
            WorkloadLevel.Low => "??",
            WorkloadLevel.Medium => "??",
            WorkloadLevel.High => "??",
            _ => "?"
        };
    }

    /// <summary>
    /// Dettaglio minimale di un ticket per il workload
    /// </summary>
    public class TicketWorkloadItem
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public string Company { get; set; }
        public TimeOnly? Time { get; set; }
        public TicketPriorities? Priority { get; set; }
    }

    /// <summary>
    /// Livelli di carico di lavoro
    /// </summary>
    public enum WorkloadLevel
    {
        Free = 0,      // 0 ticket
        Low = 1,       // 1-2 ticket
        Medium = 2,    // 3-5 ticket
        High = 3       // 6+ ticket
    }
}
