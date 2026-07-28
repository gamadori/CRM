using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CRM.Shared
{
    /// <summary>
    /// Configurazione dello smistamento automatico dei ticket verso i gruppi.
    /// Riga unica (Id = 1): sta in una tabella sua e non nelle impostazioni globali perche' e'
    /// una funzione a se', con parametri che si toccano mentre la si tara.
    /// </summary>
    public class TicketRoutingSetting
    {
        /// <summary>Identificativo della riga di configurazione: ne esiste una sola, con Id 1.</summary>
        public const int SingletonId = 1;

        public int Id { get; set; }

        [Display(Name = "Smistamento AI attivo")]
        public bool Enabled { get; set; }

        /// <summary>
        /// Sopra questa confidenza il gruppo viene assegnato da solo; sotto, il ticket resta in coda
        /// con il suggerimento visibile. Un errore dell'AI non deve mai nascondere un ticket.
        /// </summary>
        [Display(Name = "Soglia di assegnazione automatica")]
        [Range(0.5, 1.0, ErrorMessage = "La soglia deve essere compresa tra 0,50 e 1,00")]
        public double AutoAssignThreshold { get; set; } = 0.75;

        /// <summary>
        /// Limita i candidati ai gruppi collegati al tipo di ticket (Tipi ticket &gt; Gruppi).
        /// Tenerlo attivo riduce drasticamente le scelte sbagliate; disattivarlo ha senso solo se
        /// la mappa tipo-gruppo non e' compilata.
        /// </summary>
        [Display(Name = "Considera solo i gruppi abilitati al tipo di ticket")]
        public bool RestrictToTicketTypeGroups { get; set; } = true;

        /// <summary>Gruppo usato quando l'AI non decide o non e' disponibile. Null = il ticket resta in coda.</summary>
        [Display(Name = "Gruppo di ripiego")]
        [ForeignKey(nameof(FallbackGroup))]
        public int? IdFallbackGroup { get; set; }

        [Display(Name = "Smista anche i ticket aperti dalle email in arrivo")]
        public bool ApplyToEmailTickets { get; set; } = true;

        [Display(Name = "Avvisa il gruppo assegnato")]
        public bool NotifyGroupOnAssign { get; set; } = true;

        /// <summary>Modello Claude da usare; vuoto = quello configurato per il resto dell'applicazione.</summary>
        [Display(Name = "Modello")]
        [MaxLength(100)]
        public string? Model { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(450)]
        public string? UpdatedBy { get; set; }

        [JsonIgnore]
        public virtual Group? FallbackGroup { get; set; }
    }
}
