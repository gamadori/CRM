using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CRM.Server.Authentication
{
    /// <summary>
    /// Registra un provider di login esterno per ogni voce configurata. Tutto passa dal gestore
    /// OpenID Connect standard: Entra ID, Google e qualunque altro provider OIDC si aggiungono
    /// con una voce di configurazione, senza toccare il codice.
    /// Nessun provider configurato = nessuno schema registrato = login invariato.
    /// </summary>
    internal static class ExternalAuthenticationExtensions
    {
        private static readonly string[] BaseScopes = { "openid", "profile", "email" };

        /// <summary>Claim del tenant Entra, nella forma grezza e in quella mappata da .NET.</summary>
        private const string TenantIdClaim = "tid";
        private const string TenantIdClaimMapped = "http://schemas.microsoft.com/identity/claims/tenantid";

        public static AuthenticationBuilder AddExternalProviders(
            this AuthenticationBuilder builder,
            IConfiguration configuration)
        {
            var options = configuration
                .GetSection(ExternalAuthenticationOptions.SectionName)
                .Get<ExternalAuthenticationOptions>() ?? new ExternalAuthenticationOptions();

            foreach (var provider in options.Providers.Where(p => p.Enabled))
            {
                Validate(provider);
                Register(builder, provider);
            }

            return builder;
        }

        /// <summary>
        /// Un provider dichiarato attivo ma incompleto è un errore di configurazione, non una
        /// condizione da ignorare: meglio fermarsi all'avvio con un messaggio preciso che
        /// lasciare un pulsante che non comparirà mai, senza spiegazioni.
        /// Un provider con Enabled=false viene semplicemente saltato.
        /// </summary>
        private static void Validate(ExternalAuthenticationProvider p)
        {
            var mancanti = new List<string>();
            if (string.IsNullOrWhiteSpace(p.Scheme)) mancanti.Add(nameof(p.Scheme));
            if (string.IsNullOrWhiteSpace(p.Authority)) mancanti.Add(nameof(p.Authority));
            if (string.IsNullOrWhiteSpace(p.ClientId)) mancanti.Add(nameof(p.ClientId));
            if (string.IsNullOrWhiteSpace(p.ClientSecret)) mancanti.Add(nameof(p.ClientSecret));

            if (mancanti.Count > 0)
                throw new InvalidOperationException(
                    $"Provider di login esterno '{(string.IsNullOrWhiteSpace(p.Scheme) ? "(senza schema)" : p.Scheme)}' " +
                    $"abilitato ma incompleto: mancano {string.Join(", ", mancanti)}. " +
                    $"Completare la sezione {ExternalAuthenticationOptions.SectionName} oppure impostare Enabled = false.");
        }

        private static void Register(AuthenticationBuilder builder, ExternalAuthenticationProvider provider)
        {
            var displayName = string.IsNullOrWhiteSpace(provider.DisplayName) ? provider.Scheme : provider.DisplayName;

            builder.AddOpenIdConnect(provider.Scheme, displayName, o =>
            {
                o.Authority = provider.Authority;
                o.ClientId = provider.ClientId;
                o.ClientSecret = provider.ClientSecret;

                o.ResponseType = OpenIdConnectResponseType.Code;
                o.UsePkce = true;
                o.SaveTokens = false;                  // al CRM serve l'identità, non i token del provider
                o.GetClaimsFromUserInfoEndpoint = false;

                // L'identità esterna atterra in un cookie temporaneo: è Identity a decidere se e a
                // quale utente CRM corrisponde. Senza questo, il provider entrerebbe direttamente.
                o.SignInScheme = IdentityConstants.ExternalScheme;
                o.CallbackPath = $"/signin-{provider.Scheme.ToLowerInvariant()}";
                o.SignedOutCallbackPath = $"/signout-callback-{provider.Scheme.ToLowerInvariant()}";

                o.Scope.Clear();
                foreach (var scope in BaseScopes.Concat(provider.Scopes).Distinct(StringComparer.OrdinalIgnoreCase))
                    o.Scope.Add(scope);

                // La mappatura standard dei claim resta attiva di proposito: SignInManager cerca
                // ClaimTypes.NameIdentifier per costruire la chiave del collegamento, e senza
                // mappatura il claim resterebbe 'sub' e il login esterno non si aggancerebbe.

                if (provider.AllowedTenantIds.Count > 0)
                {
                    var ammessi = provider.AllowedTenantIds;
                    o.Events.OnTokenValidated = context => ValidateTenant(context, ammessi);
                }
            });
        }

        /// <summary>
        /// Con un'authority multi-tenant il provider autentica qualunque account aziendale
        /// esistente: è l'elenco dei tenant ammessi a decidere chi appartiene a questa
        /// installazione. Senza questo controllo il login sarebbe aperto al mondo.
        /// </summary>
        private static Task ValidateTenant(TokenValidatedContext context, List<string> ammessi)
        {
            var tenantId = context.Principal?.FindFirstValue(TenantIdClaim)
                ?? context.Principal?.FindFirstValue(TenantIdClaimMapped);

            if (string.IsNullOrWhiteSpace(tenantId) ||
                !ammessi.Any(t => string.Equals(t, tenantId, StringComparison.OrdinalIgnoreCase)))
            {
                context.Fail("L'organizzazione di provenienza non è autorizzata ad accedere a questa installazione.");
            }

            return Task.CompletedTask;
        }
    }
}
