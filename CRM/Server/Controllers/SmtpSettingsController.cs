using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SmtpSettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpFactory;

        public SmtpSettingsController(ApplicationDbContext context, IHttpClientFactory httpFactory)
        {
            _context = context;
            _httpFactory = httpFactory;
        }

        // GET: api/SmtpSettings -> canale a priorità più alta (retrocompatibilità)
        [HttpGet]
        public async Task<ActionResult<SmtpSetting?>> Get()
        {
            var item = await _context.SmtpSettings
                .OrderBy(s => s.Priority).ThenBy(s => s.Id)
                .FirstOrDefaultAsync() ?? new SmtpSetting();

            return Ok(item);
        }

        // GET: api/SmtpSettings/list -> tutti i canali in ordine di priorità
        [HttpGet("list")]
        public async Task<ActionResult<List<SmtpSetting>>> GetList()
        {
            var items = await _context.SmtpSettings
                .OrderBy(s => s.Priority).ThenBy(s => s.Id)
                .ToListAsync();

            return Ok(items);
        }

        // GET: api/SmtpSettings/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SmtpSetting>> Get(int id)
        {
            var smtpSettings = await _context.SmtpSettings.FindAsync(id);
            if (smtpSettings == null)
                return NotFound();

            return smtpSettings;
        }

        // PUT: api/SmtpSettings/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSmtpSettings(int id, SmtpSetting smtpSettings)
        {
            if (id != smtpSettings.Id)
                return BadRequest();

            _context.Entry(smtpSettings).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SmtpSettingsExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // POST: api/SmtpSettings
        [HttpPost]
        public async Task<ActionResult<SmtpSetting>> PostSmtpSettings(SmtpSetting smtpSettings)
        {
            // Nuovo canale in coda alla catena se la priorità non è stata impostata esplicitamente.
            if (smtpSettings.Priority == 0 && await _context.SmtpSettings.AnyAsync())
                smtpSettings.Priority = await _context.SmtpSettings.MaxAsync(s => s.Priority) + 1;

            _context.SmtpSettings.Add(smtpSettings);
            await _context.SaveChangesAsync();

            return Ok(smtpSettings);
        }

        // DELETE: api/SmtpSettings/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSmtpSettings(int id)
        {
            var smtpSettings = await _context.SmtpSettings.FindAsync(id);
            if (smtpSettings == null)
                return NotFound();

            _context.SmtpSettings.Remove(smtpSettings);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/SmtpSettings/reorder -> riassegna le priorità secondo l'ordine degli id ricevuti
        [HttpPost("reorder")]
        public async Task<IActionResult> Reorder([FromBody] List<int> orderedIds)
        {
            if (orderedIds == null || orderedIds.Count == 0)
                return BadRequest();

            var items = await _context.SmtpSettings
                .Where(s => orderedIds.Contains(s.Id))
                .ToListAsync();

            foreach (var item in items)
                item.Priority = orderedIds.IndexOf(item.Id);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/SmtpSettings/Test -> verifica il canale (connettività SMTP o validità API key)
        [HttpPost("Test")]
        public async Task<IActionResult> TestSmtp([FromBody] SmtpSetting settings)
        {
            try
            {
                if (settings.Provider == EmailProvider.Brevo)
                {
                    await TestBrevoAsync(settings);
                    return Ok(new { Success = true, Message = "API key Brevo valida." });
                }

                if (settings.Provider == EmailProvider.SendGrid)
                {
                    await TestSendGridAsync(settings);
                    return Ok(new { Success = true, Message = "API key SendGrid valida." });
                }

                using var smtp = new MailKit.Net.Smtp.SmtpClient();
                System.Net.ServicePointManager.ServerCertificateValidationCallback =
                    (sender, certificate, chain, sslPolicyErrors) => true;
                await smtp.ConnectAsync(settings.Server, settings.Port, settings.Ssl);
                if (!string.IsNullOrEmpty(settings.Username))
                    await smtp.AuthenticateAsync(settings.Username, settings.Password);
                await smtp.DisconnectAsync(true);

                return Ok(new { Success = true, Message = "Connessione SMTP riuscita." });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        private async Task TestBrevoAsync(SmtpSetting settings)
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
                throw new System.Exception("API key mancante.");

            using var client = _httpFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.brevo.com/v3/account");
            request.Headers.Add("api-key", settings.ApiKey);

            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new System.Exception($"Brevo {(int)response.StatusCode}: {body}");
            }
        }

        private async Task TestSendGridAsync(SmtpSetting settings)
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
                throw new System.Exception("API key mancante.");

            using var client = _httpFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.sendgrid.com/v3/scopes");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ApiKey);

            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new System.Exception($"SendGrid {(int)response.StatusCode}: {body}");
            }
        }

        private bool SmtpSettingsExists(int id)
        {
            return _context.SmtpSettings.Any(e => e.Id == id);
        }
    }
}
