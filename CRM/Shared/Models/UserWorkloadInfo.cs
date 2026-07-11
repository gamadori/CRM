using System;
using System.Collections.Generic;

namespace CRM.Shared.Models
{
    /// <summary>
    /// DTO che rappresenta il carico di lavoro (workload) di un utente in una data specifica.
    /// Espone anche le proprietà di presentazione (etichetta, classe CSS, percentuale barra)
    /// così che la UI resti priva di logica di soglia.
    /// </summary>
    public class UserWorkloadInfo
    {
        /// <summary>Numero di ticket a partire dal quale l'utente è considerato in sovraccarico.</summary>
        public const int OverloadThreshold = 6;

        /// <summary>ID dell'utente.</summary>
        public string UserId { get; set; }

        /// <summary>Nome completo dell'utente.</summary>
        public string FullName { get; set; }

        /// <summary>Numero totale di ticket assegnati in quella giornata.</summary>
        public int TicketCount { get; set; }

        /// <summary>Lista dei ticket assegnati (con dettagli minimi).</summary>
        public List<TicketWorkloadItem> Tickets { get; set; } = new();

        /// <summary>Livello di carico calcolato automaticamente in base al numero di ticket.</summary>
        public WorkloadLevel Level => TicketCount switch
        {
            0 => WorkloadLevel.Free,
            >= 1 and <= 2 => WorkloadLevel.Low,
            >= 3 and <= 5 => WorkloadLevel.Medium,
            _ => WorkloadLevel.High
        };

        /// <summary>Classe CSS del livello (colore di dot, barra e testo).</summary>
        public string BadgeClass => Level switch
        {
            WorkloadLevel.Free => "workload-free",
            WorkloadLevel.Low => "workload-low",
            WorkloadLevel.Medium => "workload-medium",
            WorkloadLevel.High => "workload-high",
            _ => "workload-free"
        };

        /// <summary>Etichetta breve del livello (Libero / Basso / Medio / Alto).</summary>
        public string ShortLabel => Level switch
        {
            WorkloadLevel.Free => "Libero",
            WorkloadLevel.Low => "Basso",
            WorkloadLevel.Medium => "Medio",
            WorkloadLevel.High => "Alto",
            _ => "Libero"
        };

        /// <summary>Etichetta descrittiva del livello (Libero / Carico basso / Carico medio / Sovraccarico).</summary>
        public string LevelLabel => Level switch
        {
            WorkloadLevel.Free => "Libero",
            WorkloadLevel.Low => "Carico basso",
            WorkloadLevel.Medium => "Carico medio",
            WorkloadLevel.High => "Sovraccarico",
            _ => "Libero"
        };

        /// <summary>Conteggio ticket formattato (es. "0 ticket", "1 ticket", "7 ticket").</summary>
        public string BadgeText => $"{TicketCount} ticket";

        /// <summary>
        /// Riempimento della barra di carico, 0-100. Satura a 100 una volta raggiunta
        /// la soglia di sovraccarico, così la barra piena indica "capacità esaurita".
        /// </summary>
        public int LoadPercentage => TicketCount <= 0
            ? 0
            : Math.Min(100, (int)Math.Round(TicketCount / (double)OverloadThreshold * 100));

        /// <summary>
        /// Larghezza della barra da usare nella UI: come <see cref="LoadPercentage"/> ma con un
        /// minimo visibile, così il colore del livello resta sempre leggibile anche a carico nullo.
        /// </summary>
        public int BarWidthPercentage => Math.Max(LoadPercentage, 8);
    }

    /// <summary>Dettaglio minimale di un ticket per il workload.</summary>
    public class TicketWorkloadItem
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public string Company { get; set; }
        public TimeOnly? Time { get; set; }
        public TicketPriorities? Priority { get; set; }
    }

    /// <summary>Livelli di carico di lavoro.</summary>
    public enum WorkloadLevel
    {
        Free = 0,      // 0 ticket
        Low = 1,       // 1-2 ticket
        Medium = 2,    // 3-5 ticket
        High = 3       // 6+ ticket
    }
}
