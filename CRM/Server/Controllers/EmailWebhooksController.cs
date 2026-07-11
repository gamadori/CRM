using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CRM.Server.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Riceve gli eventi di engagement dai provider ESP (aperture/click/bounce/...). Pubblico
    /// (i provider chiamano senza le nostre credenziali) ma protetto da un token segreto passato
    /// in query string: configurare l'URL del webhook come <c>.../api/EmailWebhooks/{provider}?token=SEGRETO</c>
    /// con il valore di <c>EmailWebhooks:Secret</c> (appsettings/secrets).
    /// </summary>
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class EmailWebhooksController : ControllerBase
    {
        private readonly IEmailEngagementService _engagement;
        private readonly IConfiguration _config;

        public EmailWebhooksController(IEmailEngagementService engagement, IConfiguration config)
        {
            _engagement = engagement;
            _config = config;
        }

        [HttpPost("sendgrid")]
        public async Task<IActionResult> SendGrid([FromBody] JsonElement payload, [FromQuery] string? token, CancellationToken ct)
        {
            if (!ValidToken(token)) return Unauthorized();

            var received = await _engagement.IngestSendGridAsync(payload, ct);
            return Ok(new { received });
        }

        [HttpPost("brevo")]
        public async Task<IActionResult> Brevo([FromBody] JsonElement payload, [FromQuery] string? token, CancellationToken ct)
        {
            if (!ValidToken(token)) return Unauthorized();

            var received = await _engagement.IngestBrevoAsync(payload, ct);
            return Ok(new { received });
        }

        private bool ValidToken(string? token)
        {
            var secret = _config["EmailWebhooks:Secret"];
            return !string.IsNullOrEmpty(secret) && string.Equals(secret, token, StringComparison.Ordinal);
        }
    }
}
