using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>Che cosa e' un pezzo elettronico della macchina.</summary>
    public enum MachineComponentKind
    {
        [Display(Name = "HMI")]
        Hmi = 1,

        [Display(Name = "PLC")]
        Plc = 2,

        [Display(Name = "Scheda")]
        Board = 3,

        [Display(Name = "Altro")]
        Other = 99
    }

    /// <summary>
    /// Un pezzo elettronico di una macchina, con la versione che ha adesso: un PLC, una scheda
    /// custom, un pannello HMI. Una linea ne ha piu' d'uno di ogni tipo, quindi non possono stare
    /// come campi sulla macchina.
    /// <para>
    /// L'identita' e' <b>la posizione</b> (<see cref="Code"/>: <c>PLC-1</c>, <c>HMI-ingresso</c>),
    /// non il pezzo fisico. Sostituendo un PLC guasto, la storia delle versioni di quella posizione
    /// continua invece di ricominciare da capo: quando si cerca "da quando questa linea ha la
    /// 2.14", la risposta riguarda il punto della linea, non il numero di serie che c'era dentro.
    /// </para>
    /// </summary>
    public class MachineComponent
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Article))]
        public int IdArticle { get; set; }

        /// <summary>La posizione lungo la linea. Unica dentro la macchina.</summary>
        [Required, MaxLength(80)]
        [Display(Name = "Codice posizione")]
        public string Code { get; set; } = string.Empty;

        [Display(Name = "Tipo")]
        public MachineComponentKind Kind { get; set; } = MachineComponentKind.Other;

        /// <summary>Come lo chiama chi ci lavora: "PLC nastro di carico".</summary>
        [MaxLength(150)]
        [Display(Name = "Descrizione")]
        public string? Name { get; set; }

        /// <summary>La versione vista l'ultima volta. Vuota finche' la macchina non l'ha mai detta.</summary>
        [MaxLength(80)]
        [Display(Name = "Versione")]
        public string? CurrentVersion { get; set; }

        /// <summary>Numero di serie del pezzo montato adesso, se la macchina lo sa dire.</summary>
        [MaxLength(80)]
        [Display(Name = "Matricola del pezzo")]
        public string? SerialNumber { get; set; }

        [Display(Name = "Prima volta")]
        public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Ultima volta che la macchina ha parlato di questo componente. Dice se il dato e' fresco:
        /// una versione ferma a sei mesi fa non e' una versione, e' un ricordo.
        /// </summary>
        [Display(Name = "Visto l'ultima volta")]
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

        /// <summary>Quando la versione e' cambiata l'ultima volta.</summary>
        [Display(Name = "Ultimo cambio versione")]
        public DateTime? LastVersionChangeAt { get; set; }

        public Article? Article { get; set; }
    }

    /// <summary>
    /// Un cambio di versione, come e' stato visto.
    /// <para>
    /// Non lo dichiara la macchina: lo scrive il CRM confrontando la fotografia appena arrivata con
    /// quella di prima. Se la macchina mandasse gli eventi, un aggiornamento fatto con la rete giu'
    /// resterebbe ignoto per sempre e il CRM continuerebbe a mostrare una versione che non esiste
    /// piu'.
    /// </para>
    /// </summary>
    public class MachineComponentVersionChange
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Component))]
        public int IdMachineComponent { get; set; }

        /// <summary>Versione precedente. Vuota se e' la prima volta che si vede il componente.</summary>
        [MaxLength(80)]
        public string? FromVersion { get; set; }

        [MaxLength(80)]
        public string? ToVersion { get; set; }

        /// <summary>Quando il CRM se n'e' accorto, non quando l'aggiornamento e' stato fatto.</summary>
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Chi ha portato la notizia: la chiave della macchina, o l'utente che ha corretto a mano.</summary>
        [MaxLength(120)]
        public string? Source { get; set; }

        public MachineComponent? Component { get; set; }
    }
}
