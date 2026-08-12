using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRM.Server.Data;
using CRM.Shared;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Caselle di posta in ingresso. <b>Solo Admin</b>, per lo stesso motivo di
    /// <see cref="SmtpSettingsController"/>: la password IMAP della casella assistenza apre la
    /// posta dell'azienda, e chi puo' riscrivere host e credenziali dirotta tutto quello che
    /// entra nel CRM. Con il solo login era alla portata di qualsiasi utente.
    /// <para>
    /// La password non torna mai al client (vedi <see cref="EmailInbox.HasPassword"/>); vuota in
    /// salvataggio vale "tieni quella salvata". Il <see cref="EmailInbox.WebhookToken"/> invece
    /// resta visibile: e' un valore che l'amministratore deve poter copiare nella configurazione
    /// del provider, e ormai lo vede solo lui.
    /// </para>
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "AdminRole")]
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
            var item = await _context.EmailInboxes.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new EmailInbox();
            return Ok(WithoutSecrets(item));
        }

        [HttpGet("list")]
        public async Task<ActionResult<List<EmailInbox>>> GetList()
        {
            var items = await _context.EmailInboxes.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync();
            return Ok(items.Select(WithoutSecrets).ToList());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<EmailInbox>> Get(int id)
        {
            var item = await _context.EmailInboxes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();
            return WithoutSecrets(item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, EmailInbox item)
        {
            if (id != item.Id) return BadRequest();

            // Riga tracciata invece di una staccata: e' cosi' che si tiene la password gia'
            // salvata quando la maschera la rimanda vuota.
            var stored = await _context.EmailInboxes.FirstOrDefaultAsync(x => x.Id == id);
            if (stored == null) return NotFound();

            stored.Name = item.Name;
            stored.IsActive = item.IsActive;
            stored.Mode = item.Mode;
            stored.Address = item.Address;
            stored.DefaultAction = item.DefaultAction;
            stored.IdDefaultType = item.IdDefaultType;
            stored.IdDefaultOwner = item.IdDefaultOwner;
            stored.UseAiTriage = item.UseAiTriage;
            stored.Host = item.Host;
            stored.Port = item.Port;
            stored.Ssl = item.Ssl;
            stored.Username = item.Username;
            stored.Folder = item.Folder;
            stored.PollingSeconds = item.PollingSeconds;
            stored.Provider = item.Provider;
            stored.WebhookToken = item.WebhookToken;

            if (!string.IsNullOrEmpty(item.Password))
                stored.Password = item.Password;

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

            // Si stacca prima di svuotare, altrimenti EF leggerebbe l'azzeramento come modifica.
            _context.Entry(item).State = EntityState.Detached;

            return Ok(WithoutSecrets(item));
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
                // La maschera non ha la password: per una casella gia' salvata si prova con
                // quella sul server, altrimenti il test fallirebbe sempre.
                await FillStoredSecretsAsync(inbox);

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

        /// <summary>
        /// Svuota la password prima di mandare la casella al client, lasciando solo
        /// l'informazione "ce n'e' una salvata". L'oggetto deve essere staccato dal contesto.
        /// </summary>
        private static EmailInbox WithoutSecrets(EmailInbox item)
        {
            item.HasPassword = !string.IsNullOrEmpty(item.Password);
            item.Password = null;

            return item;
        }

        /// <summary>
        /// Rimette la password salvata su una casella arrivata dal client che ne e' priva. Vale
        /// solo per le caselle gia' esistenti.
        /// </summary>
        private async Task FillStoredSecretsAsync(EmailInbox inbox)
        {
            if (inbox.Id <= 0 || !string.IsNullOrEmpty(inbox.Password))
                return;

            var stored = await _context.EmailInboxes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == inbox.Id);

            if (stored != null)
                inbox.Password = stored.Password;
        }
    }
}
