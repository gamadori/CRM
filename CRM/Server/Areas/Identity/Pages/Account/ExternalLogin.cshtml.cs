#nullable disable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CRM.Server.Authentication;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRM.Server.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Ritorno dal provider esterno. Il CRM non crea utenti da qui: un <see cref="ApplicationUser"/>
    /// ha azienda, ruolo e gruppi che governano permessi e fasi di commessa, e un'identità
    /// Microsoft o Google non ne porta nessuno. Chi non è già censito viene respinto con un
    /// messaggio chiaro, non registrato al volo in uno stato indefinito.
    /// </summary>
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ExternalLoginModel> _logger;
        private readonly ExternalAuthenticationOptions _externalOptions;

        public ExternalLoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<ExternalLoginModel> logger,
            IOptions<ExternalAuthenticationOptions> externalOptions)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _externalOptions = externalOptions.Value;
        }

        public string ProviderDisplayName { get; set; }

        public string ReturnUrl { get; set; }

        /// <summary>Titolo del riquadro mostrato quando l'accesso non va a buon fine.</summary>
        public string OutcomeTitle { get; set; }

        /// <summary>Spiegazione per l'utente: cosa è successo e cosa può fare.</summary>
        public string OutcomeDetail { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public IActionResult OnGetAsync() => RedirectToPage("./Login");

        /// <summary>Avvia il giro verso il provider.</summary>
        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl ??= Url.Content("~/");
            ReturnUrl = returnUrl;

            if (remoteError != null)
            {
                ErrorMessage = $"Errore dal provider esterno: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Non è stato possibile leggere le informazioni di accesso dal provider esterno.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            ProviderDisplayName = info.ProviderDisplayName ?? info.LoginProvider;

            // Identità già collegata a un utente: è il caso normale, dal secondo accesso in poi.
            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("Accesso con provider {Provider} riuscito.", info.LoginProvider);
                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
                return RedirectToPage("./Lockout");

            if (result.IsNotAllowed)
                return Blocked(
                    "Account non abilitato all'accesso",
                    "L'identità è collegata a un utente del CRM che non è ancora abilitato ad accedere. Contatta l'amministratore.");

            return await HandleUnlinkedIdentityAsync(info, returnUrl);
        }

        /// <summary>
        /// Primo accesso con questa identità: si applica la politica di aggancio configurata
        /// per il provider. Vedi <see cref="ExternalLoginPolicy"/>.
        /// </summary>
        private async Task<IActionResult> HandleUnlinkedIdentityAsync(ExternalLoginInfo info, string returnUrl)
        {
            var provider = _externalOptions.Providers
                .FirstOrDefault(p => string.Equals(p.Scheme, info.LoginProvider, StringComparison.OrdinalIgnoreCase));

            var linking = provider?.EmailLinking ?? ExternalEmailLinking.Disabled;
            var email = ReadEmail(info);
            var user = string.IsNullOrWhiteSpace(email) ? null : await _userManager.FindByEmailAsync(email);

            var decision = ExternalLoginPolicy.Decide(
                linking,
                providerVerifiedEmail: HasVerifiedEmail(info),
                crmUserExists: user != null);

            switch (decision)
            {
                case ExternalLoginDecision.Link:
                    return await LinkAndSignInAsync(user, info, returnUrl);

                case ExternalLoginDecision.RequireManualLink:
                    return Blocked(
                        "Collegamento non ancora effettuato",
                        $"Esiste un utente del CRM con questo indirizzo, ma non è ancora collegato a {ProviderDisplayName}. " +
                        "Accedi una volta con le tue credenziali e collega l'account dalla tua area personale: " +
                        "dalla volta successiva entrerai con un clic.");

                case ExternalLoginDecision.EmailNotVerified:
                    return Blocked(
                        "Indirizzo email non verificato",
                        $"{ProviderDisplayName} non garantisce che l'indirizzo appartenga davvero a te, quindi il " +
                        "collegamento automatico non è consentito. Accedi con le tue credenziali e collega l'account " +
                        "dalla tua area personale.");

                default:
                    _logger.LogWarning(
                        "Accesso esterno rifiutato: nessun utente CRM per l'identità {Provider} ricevuta.",
                        info.LoginProvider);
                    return Blocked(
                        "Utente non presente nel CRM",
                        "Il tuo account è stato riconosciuto, ma non risulta un utente corrispondente in questo CRM. " +
                        "L'accesso va abilitato da un amministratore.");
            }
        }

        private async Task<IActionResult> LinkAndSignInAsync(ApplicationUser user, ExternalLoginInfo info, string returnUrl)
        {
            var addLogin = await _userManager.AddLoginAsync(user, info);
            if (!addLogin.Succeeded)
            {
                _logger.LogError("Collegamento dell'identità {Provider} fallito: {Errori}",
                    info.LoginProvider, string.Join("; ", addLogin.Errors.Select(e => e.Description)));

                return Blocked(
                    "Collegamento non riuscito",
                    "Non è stato possibile collegare l'account esterno all'utente del CRM. Riprova o contatta l'amministratore.");
            }

            // L'organizzazione ha già verificato l'identità: senza questo, un utente con email non
            // confermata resterebbe fuori per sempre, perché l'installazione richiede la conferma
            // e da un login esterno non passa nessuna email di verifica.
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            // Si rientra dal percorso standard, così valgono comunque blocchi e requisiti dell'account.
            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("Identità {Provider} collegata all'utente {UserId} e accesso effettuato.",
                    info.LoginProvider, user.Id);
                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
                return RedirectToPage("./Lockout");

            return Blocked(
                "Account non abilitato all'accesso",
                "L'identità è stata collegata, ma l'utente del CRM non è abilitato ad accedere. Contatta l'amministratore.");
        }

        /// <summary>
        /// Email dichiarata dal provider. Entra ID può esporla come <c>email</c> oppure, per gli
        /// account di lavoro, solo come <c>preferred_username</c>.
        /// </summary>
        private static string ReadEmail(ExternalLoginInfo info)
            => info.Principal.FindFirstValue(ClaimTypes.Email)
               ?? info.Principal.FindFirstValue("email")
               ?? info.Principal.FindFirstValue("preferred_username");

        private static bool HasVerifiedEmail(ExternalLoginInfo info)
            => string.Equals(info.Principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);

        private PageResult Blocked(string titolo, string dettaglio)
        {
            OutcomeTitle = titolo;
            OutcomeDetail = dettaglio;
            return Page();
        }
    }
}
