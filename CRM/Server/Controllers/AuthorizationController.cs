using CRM.Server.Authentication;
using CRM.Shared;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CRM.Server.Controllers
{
    public class AuthorizationController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AuthorizationController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [AllowAnonymous]
        [HttpGet("~/connect/authorize")]
        [HttpPost("~/connect/authorize")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Authorize()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("Richiesta OpenID Connect non disponibile.");
            var authentication = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

            if (!authentication.Succeeded)
            {
                if (request.HasPromptValue(PromptValues.None))
                {
                    return Forbid(
                        new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "È necessario effettuare il login."
                        }),
                        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                }

                return Challenge(
                    new AuthenticationProperties
                    {
                        RedirectUri = Request.PathBase + Request.Path + Request.QueryString
                    },
                    IdentityConstants.ApplicationScheme);
            }

            var user = await _userManager.GetUserAsync(authentication.Principal);
            if (user == null || !await _signInManager.CanSignInAsync(user))
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var principal = await CreatePrincipalAsync(user, request.GetScopes());
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        [AllowAnonymous]
        [HttpPost("~/connect/token")]
        [IgnoreAntiforgeryToken]
        [Produces("application/json")]
        public async Task<IActionResult> Exchange()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("Richiesta OpenID Connect non disponibile.");

            if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
            {
                return BadRequest(new { error = Errors.UnsupportedGrantType });
            }

            var authentication = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var subject = authentication.Principal?.GetClaim(Claims.Subject);
            var user = subject == null ? null : await _userManager.FindByIdAsync(subject);

            if (user == null || !await _signInManager.CanSignInAsync(user))
            {
                return Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "L'account non è più disponibile."
                    }),
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            // Questo principal contiene anche i metadati protocollo del codice o
            // del refresh token. OpenIddict deve riceverlo senza ricostruzioni.
            return SignIn(
                authentication.Principal!,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
        [HttpGet("~/connect/userinfo")]
        [HttpPost("~/connect/userinfo")]
        [Produces("application/json")]
        public async Task<IActionResult> UserInfo()
        {
            var subject = User.GetClaim(Claims.Subject);
            var user = subject == null ? null : await _userManager.FindByIdAsync(subject);

            if (user == null || !await _signInManager.CanSignInAsync(user))
            {
                return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var claims = new Dictionary<string, object?>
            {
                [Claims.Subject] = await _userManager.GetUserIdAsync(user),
                [Claims.Name] = await _userManager.GetUserNameAsync(user)
            };

            if (User.HasScope(Scopes.Email))
            {
                claims[Claims.Email] = await _userManager.GetEmailAsync(user);
            }

            if (User.HasScope(Scopes.Roles))
            {
                claims[Claims.Role] = await _userManager.GetRolesAsync(user);
            }

            return Ok(claims);
        }

        [AllowAnonymous]
        [HttpGet("~/connect/logout")]
        [HttpPost("~/connect/logout")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Logout()
        {
            var request = HttpContext.GetOpenIddictServerRequest();
            await _signInManager.SignOutAsync();
            return SignOut(
                new AuthenticationProperties { RedirectUri = request?.PostLogoutRedirectUri ?? "/" },
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        private async Task<ClaimsPrincipal> CreatePrincipalAsync(ApplicationUser user, IEnumerable<string> scopes)
        {
            var identity = new ClaimsIdentity(
                TokenValidationParameters.DefaultAuthenticationType,
                Claims.Name,
                Claims.Role);

            identity.SetClaim(Claims.Subject, await _userManager.GetUserIdAsync(user));
            identity.SetClaim(Claims.Name, await _userManager.GetUserNameAsync(user));
            identity.SetClaim(Claims.Email, await _userManager.GetEmailAsync(user));

            foreach (var role in await _userManager.GetRolesAsync(user))
            {
                identity.AddClaim(new Claim(Claims.Role, role));
            }

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(scopes);
            principal.SetResources(CRM.Server.Authentication.OpenIddictConfiguration.ApiScope);
            principal.SetDestinations(static claim => claim.Type switch
            {
                Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
                Claims.Name => [Destinations.AccessToken, Destinations.IdentityToken],
                Claims.Email when claim.Subject.HasScope(Scopes.Email) => [Destinations.AccessToken, Destinations.IdentityToken],
                Claims.Role when claim.Subject.HasScope(Scopes.Roles) => [Destinations.AccessToken, Destinations.IdentityToken],
                _ => [Destinations.AccessToken]
            });

            return principal;
        }

    }
}
