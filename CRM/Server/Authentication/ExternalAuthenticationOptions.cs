using System.Collections.Generic;

namespace CRM.Server.Authentication
{
    /// <summary>
    /// Politica di aggancio fra un'identità esterna e un utente del CRM al primo accesso.
    /// </summary>
    public enum ExternalEmailLinking
    {
        /// <summary>
        /// Nessun aggancio automatico: l'utente entra con le credenziali locali e collega
        /// l'identità esterna dalla propria area. È il default, ed è la scelta giusta per i
        /// provider aperti al pubblico (Google, Facebook), dove chiunque può farsi rilasciare
        /// un'identità con un'email qualsiasi.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Aggancio all'utente con la stessa email, ma solo se il provider dichiara di averla
        /// verificata (claim <c>email_verified</c>).
        /// </summary>
        WhenProviderVerifies = 1,

        /// <summary>
        /// Aggancio all'utente con la stessa email fidandosi del provider anche senza claim di
        /// verifica. Da usare SOLO con provider aziendali a tenant singolo — tipicamente Entra ID
        /// con <see cref="ExternalAuthenticationProvider.AllowedTenantIds"/> valorizzato — dove le
        /// identità sono create e gestite dall'organizzazione. Entra ID non emette
        /// <c>email_verified</c> per gli account di lavoro, quindi senza questo valore l'aggancio
        /// automatico non scatterebbe mai.
        /// </summary>
        TrustProvider = 2
    }

    /// <summary>
    /// Provider di login esterni (Entra ID, Google, qualunque OpenID Connect), letti da
    /// configurazione. Senza provider abilitati il login resta esattamente quello locale di oggi.
    /// I segreti stanno in configurazione e non a database: un client secret vale l'identità
    /// dell'applicazione presso il provider, e gli schemi di autenticazione si registrano
    /// all'avvio, quindi un valore a database richiederebbe comunque un riavvio.
    /// </summary>
    public class ExternalAuthenticationOptions
    {
        public const string SectionName = "ExternalAuthentication";

        public List<ExternalAuthenticationProvider> Providers { get; set; } = new();
    }

    public class ExternalAuthenticationProvider
    {
        /// <summary>
        /// Identificativo tecnico dello schema, anche nell'URL di callback (/signin-{scheme}).
        /// Una volta scelto non va cambiato: è la chiave con cui i collegamenti già fatti sono
        /// salvati in AspNetUserLogins, e rinominarlo scollegherebbe tutti gli utenti.
        /// </summary>
        public string Scheme { get; set; } = string.Empty;

        /// <summary>Nome sul pulsante di login. Es. "Microsoft".</summary>
        public string DisplayName { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Authority OpenID Connect. Per Entra ID a tenant singolo:
        /// https://login.microsoftonline.com/{tenantId}/v2.0
        /// </summary>
        public string Authority { get; set; } = string.Empty;

        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>Scope aggiuntivi oltre a openid, profile ed email.</summary>
        public List<string> Scopes { get; set; } = new();

        /// <summary>Icona Material Symbols mostrata sul pulsante.</summary>
        public string Icon { get; set; } = "login";

        /// <summary>
        /// Tenant Entra ammessi (claim <c>tid</c>). Obbligatorio nei fatti se l'authority è
        /// multi-tenant (/common, /organizations): senza questo elenco qualunque account
        /// aziendale Microsoft al mondo potrebbe autenticarsi su questa installazione.
        /// Vuoto = nessun controllo, accettabile solo con authority a tenant singolo.
        /// </summary>
        public List<string> AllowedTenantIds { get; set; } = new();

        /// <summary>Vedi <see cref="ExternalEmailLinking"/>. Default: nessun aggancio automatico.</summary>
        public ExternalEmailLinking EmailLinking { get; set; } = ExternalEmailLinking.Disabled;
    }
}
