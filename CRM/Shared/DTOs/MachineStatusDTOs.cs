using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared.DTOs
{
    /// <summary>
    /// La fotografia che una macchina manda di se stessa: com'e' fatta adesso e quanto ha lavorato.
    /// <para>
    /// Non e' un elenco di eventi. La macchina dice lo stato in cui si trova; il CRM confronta con
    /// la fotografia precedente e si scrive da solo cosa e' cambiato. Un aggiornamento fatto mentre
    /// la rete era giu' viene recuperato al primo collegamento utile, invece di restare ignoto.
    /// </para>
    /// <para>Le due parti sono indipendenti: si puo' mandare solo i componenti, o solo i contatori.</para>
    /// </summary>
    public class MachineStatusRequest
    {
        /// <summary>
        /// Tutti i componenti della macchina, non solo quelli cambiati: e' una fotografia. Un
        /// elenco vuoto (o assente) significa "non ne parlo adesso", non "non ne ho piu'".
        /// </summary>
        public List<MachineComponentStatus>? Components { get; set; }

        public MachineCountersStatus? Counters { get; set; }
    }

    public class MachineComponentStatus
    {
        /// <summary>La posizione lungo la linea: <c>PLC-1</c>, <c>HMI-ingresso</c>.</summary>
        [Required, MaxLength(80)]
        public string Code { get; set; } = string.Empty;

        public MachineComponentKind Kind { get; set; } = MachineComponentKind.Other;

        [MaxLength(150)]
        public string? Name { get; set; }

        [MaxLength(80)]
        public string? Version { get; set; }

        /// <summary>Matricola del pezzo montato, se la macchina la sa dire.</summary>
        [MaxLength(80)]
        public string? SerialNumber { get; set; }
    }

    public class MachineCountersStatus
    {
        /// <summary>
        /// Il giorno a cui si riferiscono i totali. Assente = oggi. Cosi' una macchina che manda
        /// dopo la mezzanotte puo' dichiarare il giorno appena finito.
        /// </summary>
        public DateTime? Day { get; set; }

        /// <summary>Ore totali da quando la macchina esiste, non quelle del giorno.</summary>
        public decimal? TotalHours { get; set; }

        /// <summary>Pezzi totali da quando la macchina esiste, non quelli del giorno.</summary>
        public long? TotalPieces { get; set; }
    }

    /// <summary>Cosa il CRM ha capito dalla fotografia: serve a chi la manda per accorgersi degli errori.</summary>
    public class MachineStatusResponse
    {
        public string SerialNumber { get; set; } = string.Empty;

        public int ComponentsReceived { get; set; }

        /// <summary>Componenti mai visti prima, registrati adesso.</summary>
        public List<string> NewComponents { get; set; } = new();

        /// <summary>Cambi di versione rilevati con questa fotografia.</summary>
        public List<MachineVersionChangeDTO> VersionChanges { get; set; } = new();

        public MachineDailyReadingDTO? Counters { get; set; }
    }

    public class MachineVersionChangeDTO
    {
        public string Code { get; set; } = string.Empty;

        public string? FromVersion { get; set; }

        public string? ToVersion { get; set; }
    }

    /// <summary>Quello che la scheda macchina mostra: com'e' fatta, cosa e' cambiato, quanto lavora.</summary>
    public class MachineStatusOverviewDTO
    {
        public List<MachineComponentDTO> Components { get; set; } = new();

        public List<MachineVersionChangeLogDTO> RecentChanges { get; set; } = new();

        public List<MachineDailyReadingDTO> Readings { get; set; } = new();

        /// <summary>Ore sommate sui giorni mostrati.</summary>
        public decimal? PeriodHours { get; set; }

        /// <summary>Pezzi sommati sui giorni mostrati.</summary>
        public long? PeriodPieces { get; set; }
    }

    public class MachineComponentDTO
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public MachineComponentKind Kind { get; set; }

        public string? Name { get; set; }

        public string? CurrentVersion { get; set; }

        public string? SerialNumber { get; set; }

        public DateTime LastSeenAt { get; set; }

        public DateTime? LastVersionChangeAt { get; set; }
    }

    public class MachineVersionChangeLogDTO
    {
        public string Code { get; set; } = string.Empty;

        public MachineComponentKind Kind { get; set; }

        public string? FromVersion { get; set; }

        public string? ToVersion { get; set; }

        public DateTime DetectedAt { get; set; }

        public string? Source { get; set; }
    }

    public class MachineDailyReadingDTO
    {
        public DateTime Day { get; set; }

        public decimal? TotalHours { get; set; }

        public long? TotalPieces { get; set; }

        public decimal? Hours { get; set; }

        public long? Pieces { get; set; }

        public bool CounterReset { get; set; }
    }
}
