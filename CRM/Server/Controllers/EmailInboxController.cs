using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRM.Server.Data;
using CRM.Shared;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailInboxController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EmailInboxController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<EmailInbox?>> Get()
        {
            var item = await _context.EmailInboxes.OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new EmailInbox();
            return Ok(item);
        }

        [HttpGet("list")]
        public async Task<ActionResult<List<EmailInbox>>> GetList()
        {
            var items = await _context.EmailInboxes.OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync();
            return Ok(items);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<EmailInbox>> Get(int id)
        {
            var item = await _context.EmailInboxes.FindAsync(id);
            if (item == null) return NotFound();
            return item;
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, EmailInbox item)
        {
            if (id != item.Id) return BadRequest();

            _context.Entry(item).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.EmailInboxes.Any(e => e.Id == id)) return NotFound();
                throw;
            }
            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<EmailInbox>> Post(EmailInbox item)
        {
            _context.EmailInboxes.Add(item);
            await _context.SaveChangesAsync();
            return Ok(item);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.EmailInboxes.FindAsync(id);
            if (item == null) return NotFound();

            _context.EmailInboxes.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/EmailInbox/Test -> verifica la casella (connettività IMAP o presenza token webhook)
        [HttpPost("Test")]
        public async Task<IActionResult> Test([FromBody] EmailInbox inbox)
        {
            try
            {
                if (inbox.Mode == EmailInboxMode.InboundParseEsp)
                {
                    if (string.IsNullOrWhiteSpace(inbox.WebhookToken))
                        return BadRequest(new { Success = false, Message = "Token webhook mancante." });

                    return Ok(new { Success = true, Message = "Casella inbound-parse configurata. Imposta l'URL webhook nel provider." });
                }

                using var client = new ImapClient();
                client.ServerCertificateValidationCallback = (s, c, chain, e) => true;
                var socket = inbox.Ssl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                await client.ConnectAsync(inbox.Host, inbox.Port, socket);
                await client.AuthenticateAsync(inbox.Username, inbox.Password);
                await client.DisconnectAsync(true);

                return Ok(new { Success = true, Message = "Connessione IMAP riuscita." });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }
    }
}
