namespace CRM.Server.Services.Sms
{
    /// <summary>
    /// Configurazione dell'invio SMS (sezione "Sms" di appsettings).
    /// Il provider è astratto dietro <see cref="ISmsSender"/>: cambiare fornitore
    /// significa solo aggiungere un'implementazione e cambiare "Provider".
    /// </summary>
    public class SmsOptions
    {
        public const string SectionName = "Sms";

        /// <summary>Provider attivo: "Twilio" oppure "None" (nessun invio SMS).</summary>
        public string Provider { get; set; } = "None";

        /// <summary>
        /// Prefisso internazionale applicato ai numeri privi di prefisso
        /// (es. "3331234567" → "+393331234567").
        /// </summary>
        public string DefaultCountryPrefix { get; set; } = "+39";

        public TwilioOptions Twilio { get; set; } = new();
    }

    /// <summary>Credenziali e mittente Twilio.</summary>
    public class TwilioOptions
    {
        public string AccountSid { get; set; } = string.Empty;
        public string AuthToken { get; set; } = string.Empty;

        /// <summary>Numero mittente in formato E.164 (es. "+391234567890").</summary>
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// In alternativa a <see cref="From"/>, il Messaging Service SID (MG...).
        /// Se valorizzato ha priorità sul numero mittente.
        /// </summary>
        public string MessagingServiceSid { get; set; } = string.Empty;
    }
}
