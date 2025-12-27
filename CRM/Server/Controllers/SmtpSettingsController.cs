using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Server.Services;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SmtpSettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly IEmailSenderPlus _emailService;
        public SmtpSettingsController(ApplicationDbContext context, IEmailSenderPlus emailService   )
        {
            _context = context;
            _emailService = emailService;
        }

        // GET: api/SmtpSettings
        [HttpGet]
        public async Task<ActionResult<SmtpSetting?>> Get()
        {
            return await _context.SmtpSettings.FirstOrDefaultAsync();
        }

        // GET: api/SmtpSettings/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SmtpSetting>> Get(int id)
        {
            var smtpSettings = await _context.SmtpSettings.FindAsync(id);

            if (smtpSettings == null)
            {
                return NotFound();
            }

            return smtpSettings;
        }

        // PUT: api/SmtpSettings/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSmtpSettings(int id, SmtpSetting smtpSettings)
        {
            if (id != smtpSettings.Id)
            {
                return BadRequest();
            }

            _context.Entry(smtpSettings).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SmtpSettingsExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/SmtpSettings
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<SmtpSetting>> PostSmtpSettings(SmtpSetting smtpSettings)
        {
            _context.SmtpSettings.Add(smtpSettings);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSmtpSettings", new { id = smtpSettings.Id }, smtpSettings);
        }

        // DELETE: api/SmtpSettings/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSmtpSettings(int id)
        {
            var smtpSettings = await _context.SmtpSettings.FindAsync(id);
            if (smtpSettings == null)
            {
                return NotFound();
            }

            _context.SmtpSettings.Remove(smtpSettings);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/SmtpSettings/Test
        [HttpPost("Test")]
        public async Task<IActionResult> TestSmtp([FromBody] SmtpSetting smtpSettings)
        {
            try
            {
                // Invia una mail di test a se stesso
                var to = smtpSettings.SenderEmail;
                var subject = "Test SMTP";
                var message = "Questa è una mail di test.";

                // Usa direttamente MailKit per testare la connessione
                using var smtp = new MailKit.Net.Smtp.SmtpClient();
                System.Net.ServicePointManager.ServerCertificateValidationCallback =
                    (sender, certificate, chain, sslPolicyErrors) => true;
                await smtp.ConnectAsync(smtpSettings.Server, smtpSettings.Port, smtpSettings.Ssl);
                await smtp.AuthenticateAsync(smtpSettings.Username, smtpSettings.Password);
                await smtp.DisconnectAsync(true);

                // Se vuoi inviare anche una mail reale, decommenta:
                await _emailService.SendEmailAsync(to, subject, message);

                return Ok(new { Success = true, Message = "Connessione SMTP riuscita." });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        private bool SmtpSettingsExists(int id)
        {
            return _context.SmtpSettings.Any(e => e.Id == id);
        }
    }
}
