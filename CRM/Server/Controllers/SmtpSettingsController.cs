using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Canali di invio email. <b>Solo Admin</b>: qui non si leggono soltanto le credenziali della
    /// posta aziendale, si decide da quale server esce tutta la posta del CRM. Con il solo login
    /// (la regola globale di Program.cs) chiunque avesse un account poteva leggere password SMTP e
    /// API key in chiaro, e soprattutto dirottare l'intera posta in uscita su un server proprio.
    /// <para>
    /// Le credenziali non tornano mai al client: le GET le svuotano e segnalano la loro esistenza
    /// con <see cref="SmtpSetting.HasPassword"/>/<see cref="SmtpSetting.HasApiKey"/>. Di
    /// conseguenza, in salvataggio un segreto vuoto vale "tieni quello salvato": altrimenti il
    /// primo salvataggio della maschera cancellerebbe la password senza dirlo a nessuno.
    /// </para>
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "AdminRole")]
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
                .AsNoTracking()
                .OrderBy(s => s.Priority).ThenBy(s => s.Id)
                .FirstOrDefaultAsync() ?? new SmtpSetting();

            return Ok(WithoutSecrets(item));
        }

        // GET: api/SmtpSettings/list -> tutti i canali in ordine di priorità
        [HttpGet("list")]
        public async Task<ActionResult<List<SmtpSetting>>> GetList()
        {
            var items = await _context.SmtpSettings
                .AsNoTracking()
                .OrderBy(s => s.Priority).ThenBy(s => s.Id)
                .ToListAsync();

            return Ok(items.Select(WithoutSecrets).ToList());
        }

        // GET: api/SmtpSettings/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SmtpSetting>> Get(int id)
        {
            var smtpSettings = await _context.SmtpSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (smtpSettings == null)
                return NotFound();

            return WithoutSecrets(smtpSettings);
        }

        // PUT: api/SmtpSettings/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSmtpSettings(int id, SmtpSetting smtpSettings)
        {
            if (id != smtpSettings.Id)
                return BadRequest();

            // Si aggiorna la riga tracciata invece di attaccarne una staccata: e' l'unico modo per
            // poter tenere i segreti gia' salvati quando la maschera li rimanda vuoti.
            var stored = await _context.SmtpSettings.FirstOrDefaultAsync(s => s.Id == id);
            if (stored == null)
                return NotFound();

            stored.Name = smtpSettings.Name;
            stored.Provider = smtpSettings.Provider;
            stored.Priority = smtpSettings.Priority;
            stored.IsActive = smtpSettings.IsActive;
            stored.Server = smtpSettings.Server;
            stored.Port = smtpSettings.Port;
            stored.Username = smtpSettings.Username;
            stored.Ssl = smtpSettings.Ssl;
            stored.SenderName = smtpSettings.SenderName;
            stored.SenderEmail = smtpSettings.SenderEmail;

            if (!string.IsNullOrEmpty(smtpSettings.Password))
                stored.Password = smtpSettings.Password;

            if (!string.IsNullOrEmpty(smtpSettings.ApiKey))
                stored.ApiKey = smtpSettings.ApiKey;

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

            // La riga appena creata torna al client senza i segreti che il client stesso ha appena
            // mandato: e' la stessa regola delle GET, e vale anche per l'eco della POST. Si stacca
            // prima di svuotarla, altrimenti EF vedrebbe i campi azzerati come una modifica.
            _context.Entry(smtpSettings).State = EntityState.Detached;

            return Ok(WithoutSecrets(smtpSettings));
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
                // La maschera non ha i segreti (non glieli mandiamo): per un canale gia' salvato si
                // testa con quelli sul server, altrimenti "Test canale" fallirebbe sempre.
                await FillStoredSecretsAsync(settings);

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

        /// <summary>
        /// Svuota i segreti prima di mandare il canale al client, lasciando solo l'informazione
        /// "ce n'e' uno salvato". L'oggetto passato deve essere staccato dal contesto.
        /// </summary>
        private static SmtpSetting WithoutSecrets(SmtpSetting item)
        {
            item.HasPassword = !string.IsNullOrEmpty(item.Password);
            item.HasApiKey = !string.IsNullOrEmpty(item.ApiKey);

            item.Password = string.Empty;
            item.ApiKey = null;

            return item;
        }

        /// <summary>
        /// Rimette i segreti salvati su un canale arrivato dal client che ne e' privo. Vale solo
        /// per i canali gia' esistenti: su uno nuovo non c'e' niente da recuperare.
        /// </summary>
        private async Task FillStoredSecretsAsync(SmtpSetting settings)
        {
            if (settings.Id <= 0)
                return;

            if (!string.IsNullOrEmpty(settings.Password) && !string.IsNullOrEmpty(settings.ApiKey))
                return;

            var stored = await _context.SmtpSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == settings.Id);

            if (stored == null)
                return;

            if (string.IsNullOrEmpty(settings.Password))
                settings.Password = stored.Password;

            if (string.IsNullOrEmpty(settings.ApiKey))
                settings.ApiKey = stored.ApiKey;
        }
    }
}
