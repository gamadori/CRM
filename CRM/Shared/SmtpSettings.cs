using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared
{
    /// <summary>Tipo di canale di invio email.</summary>
    public enum EmailProvider
    {
        [Display(Name = "SMTP")]
        Smtp = 0,

        [Display(Name = "Brevo")]
        Brevo = 1,

        [Display(Name = "SendGrid")]
        SendGrid = 2
    }

    /// <summary>
    /// Configurazione di un canale di invio email. La tabella contiene una <b>lista ordinata</b>
    /// di canali (per <see cref="Priority"/>): il primo attivo è il primario, gli altri sono i
    /// fallback usati dall'outbox se i precedenti falliscono. Un canale può essere SMTP o un
    /// provider API (es. Brevo).
    /// </summary>
    public class SmtpSetting
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Nome")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Tipo")]
        public EmailProvider Provider { get; set; } = EmailProvider.Smtp;

        /// <summary>Ordine nella catena di invio: 0 = primario, poi i fallback in ordine crescente.</summary>
        [Display(Name = "Priorità")]
        public int Priority { get; set; }

        [Display(Name = "Attivo")]
        public bool IsActive { get; set; } = true;

        // ---- SMTP ----

        [Display(Name = "Server")]
        public string Server { get; set; } = string.Empty;

        [Display(Name = "Porta")]
        public int Port { get; set; } = 25;

        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [PasswordPropertyText]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "SSL")]
        public bool Ssl { get; set; } = false;

        // ---- Provider API (es. Brevo) ----

        [Display(Name = "API Key")]
        public string? ApiKey { get; set; }

        // ---- Comune ----

        [Display(Name = "Nome mittente")]
        public string SenderName { get; set; } = string.Empty;

        [DataType(DataType.EmailAddress)]
        [Display(Name = "Email mittente")]
        public string SenderEmail { get; set; } = string.Empty;

        /// <summary>Etichetta leggibile del canale (per log/liste): il Nome se presente, altrimenti tipo+priorità.</summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"{Provider} #{Priority}" : Name;
    }
}
