
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public enum PropertyStates
    {
        [Display(Name = "No")]
        No,
        [Display(Name = "Opzionale")]
        Optional,

        [Display(Name = "Richiesto")]
        Required,

        [Display(Name = "Opzionale per Admin")]
        OptionalAdmin,

        [Display(Name = "Richiesto per Admin")]
        RequiredAdmin,

        [Display(Name = "Automatico")]
        Auto
    }
    public class TicketType
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = nameof(TicketType.Desc), ResourceType = typeof(Resources.Models.TicketType))]
        [Required]
        public string Desc { get; set; }

        [Display(Name = nameof(TicketType.CustomerEnabled), ResourceType = typeof(Resources.Models.TicketType))]
        public bool CustomerEnabled { get; set; }

        [Display(Name = "Data")]
        public int Date { get; set; }

        [Display(Name = "Orario")]
        public int Time { get; set; }

        [Display(Name = "DataFine")]
        public int DateEnd { get; set; }


        [Display(Name = "Prodotto")]
        public int IdProdotto { get; set; }

        [Display(Name = "Articolo")]
        public int IdArticolo { get; set; }

        [Display(Name = "Progetto")]
        public int Project { get; set; }

        [Display(Name = "Data Scadenza Ticket")]
        public int ExpiredDate { get; set; }

        [Display(Name = nameof(TicketType.MaxHoursLife), ResourceType = typeof(Resources.Models.TicketType))]
        public int? MaxHoursLife { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = nameof(TicketType.Price), ResourceType = typeof(Resources.Models.TicketType))]
        public decimal Price { get; set; }

        /// <summary>Anticipo (minuti) del preavviso appuntamento per questo tipo. Null = usa il default globale.</summary>
        [Display(Name = "Preavviso appuntamento (minuti, vuoto = globale)")]
        public int? AppointmentReminderMinutes { get; set; }

        /// <summary>Anticipo (minuti) del preavviso scadenza per questo tipo. Null = usa il default globale.</summary>
        [Display(Name = "Preavviso scadenza (minuti, vuoto = globale)")]
        public int? ExpiryReminderMinutes { get; set; }

        /// <summary>
        /// Se true il ticket non si chiude senza almeno un intervento registrato: ore, trasferte,
        /// parti montate e rapportino vivono solo negli interventi, quindi chiudere senza
        /// significa perdere il lavoro fatto.
        /// <para>
        /// Default true, da spegnere sui tipi che si chiudono legittimamente senza lavoro
        /// (duplicati, richieste informazioni): su quei tipi la modalita' di intervento resta
        /// chiesta in chiusura su <see cref="Ticket.Support"/>, perche' nessun intervento la porta.
        /// </para>
        /// </summary>
        [Display(Name = "Richiede almeno un intervento per la chiusura")]
        public bool RequiresIntervention { get; set; } = true;

        [NotMapped]
        public string Language { get; set; }
        public ICollection<ApplicationUser> Users { get; set; }

        public ICollection<Group> Groups { get; set; }

        [JsonIgnore]
        public ICollection<TicketTypeLanguage> Languages { get; set; }
        

    }

   
    public class TicketTypeViewModel
    {
        public int? Id { get; set; }

        public string Desc { get; set; }

    }

    public class TicketTypeFilter : PagingParameterModel
    {
        public string Desc { get; set; }

       
    }
}
