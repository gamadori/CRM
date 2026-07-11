using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>Modalità di ricezione di una casella: lettura diretta IMAP o inbound-parse via provider ESP.</summary>
    public enum EmailInboxMode
    {
        [Display(Name = "IMAP")]
        Imap = 0,

        [Display(Name = "Inbound-parse ESP")]
        InboundParseEsp = 1
    }

    /// <summary>Azione da applicare alle email in arrivo (routing a valle, comune ai due modi).</summary>
    public enum InboundAction
    {
        [Display(Name = "Attività sul contatto")]
        ActivityOnContact = 0,

        [Display(Name = "Nuovo ticket")]
        NewTicket = 1
    }

    /// <summary>
    /// Casella di posta in ingresso configurata da UI. La stessa tabella guida due meccanismi:
    /// le caselle <see cref="EmailInboxMode.Imap"/> vengono lette in polling dal background service;
    /// quelle <see cref="EmailInboxMode.InboundParseEsp"/> ricevono i messaggi via webhook dell'ESP.
    /// In entrambi i casi il routing a valle è unico.
    /// </summary>
    public class EmailInbox
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Nome")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Attiva")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Modalità")]
        public EmailInboxMode Mode { get; set; } = EmailInboxMode.Imap;

        /// <summary>Indirizzo della casella (es. assistenza@azienda.it): chiave di instradamento.</summary>
        [Display(Name = "Indirizzo")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "Azione")]
        public InboundAction DefaultAction { get; set; } = InboundAction.ActivityOnContact;

        /// <summary>Tipo di ticket usato quando l'azione è "Nuovo ticket" (obbligatorio per abilitare la creazione).</summary>
        [Display(Name = "Tipo ticket")]
        public int? IdDefaultType { get; set; }

        /// <summary>Utente registrato come apritore dei ticket creati da questa casella (obbligatorio per abilitare la creazione).</summary>
        [Display(Name = "Aperto da")]
        public string? IdDefaultOwner { get; set; }

        /// <summary>
        /// Se attivo, l'AI riassume l'email e valuta se è una richiesta di assistenza: il riassunto
        /// diventa la descrizione del ticket e le email non pertinenti non aprono ticket automatici
        /// (restano comunque registrate e segnalate come da leggere).
        /// </summary>
        [Display(Name = "Triage AI")]
        public bool UseAiTriage { get; set; }

        // ---- IMAP ----

        [Display(Name = "Host")]
        public string? Host { get; set; }

        [Display(Name = "Porta")]
        public int Port { get; set; } = 993;

        [Display(Name = "SSL")]
        public bool Ssl { get; set; } = true;

        [Display(Name = "Username")]
        public string? Username { get; set; }

        [PasswordPropertyText]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string? Password { get; set; }

        [Display(Name = "Cartella")]
        public string Folder { get; set; } = "INBOX";

        [Display(Name = "Intervallo (sec)")]
        public int PollingSeconds { get; set; } = 120;

        // ---- Inbound-parse ESP ----

        [Display(Name = "Provider")]
        public EmailProvider Provider { get; set; } = EmailProvider.Brevo;

        /// <summary>Token che l'ESP deve presentare nel webhook per autenticare e instradare i messaggi a questa casella.</summary>
        [Display(Name = "Token webhook")]
        public string? WebhookToken { get; set; }

        [NotMapped]
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Address : Name;
    }

    public class EmailInboxFilterModel : PagingParameterModel
    {
    }
}
