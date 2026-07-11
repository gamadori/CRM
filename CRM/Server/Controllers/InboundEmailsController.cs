using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Controllers
{
    /// <summary>Consultazione e triage delle email in ingresso registrate (incluse le "non associate").</summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class InboundEmailsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;

        public InboundEmailsController(ApplicationDbContext context, IPermitsService permitsService)
        {
            _context = context;
            _permitsService = permitsService;
        }

        [HttpGet("list")]
        public async Task<ActionResult<List<InboundEmail>>> GetList([FromQuery] bool onlyUnmatched = false, [FromQuery] bool onlyUnhandled = false)
        {
            var q = _context.InboundEmails.AsNoTracking().AsQueryable();

            if (onlyUnmatched)
                q = q.Where(x => !x.IsMatched);

            if (onlyUnhandled)
                q = q.Where(x => !x.Handled);

            var items = await q.OrderByDescending(x => x.ReceivedAt).Take(200).ToListAsync();
            return Ok(items);
        }

        // POST: api/InboundEmails/5/handled?value=true -> segna letta/da leggere
        [HttpPost("{id}/handled")]
        public async Task<IActionResult> SetHandled(int id, [FromQuery] bool value = true)
        {
            var item = await _context.InboundEmails.FindAsync(id);
            if (item == null) return NotFound();

            item.Handled = value;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/InboundEmails/mark-all-read -> segna come lette tutte le email in ingresso
        [HttpPost("mark-all-read")]
        public async Task<ActionResult<int>> MarkAllRead()
        {
            var items = await _context.InboundEmails.Where(x => !x.Handled).ToListAsync();
            foreach (var item in items)
                item.Handled = true;

            await _context.SaveChangesAsync();
            return Ok(items.Count);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InboundEmail>> Get(int id)
        {
            var item = await _context.InboundEmails.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();

            // Aprire il dettaglio significa prenderla in carico: esce dagli avvisi.
            if (!item.Handled)
            {
                item.Handled = true;
                await _context.SaveChangesAsync();
            }

            return item;
        }

        // GET: api/InboundEmails/by-ticket/12 -> email da cui è nato (o a cui è agganciato) il ticket
        [HttpGet("by-ticket/{idTicket}")]
        public async Task<ActionResult<InboundEmail?>> GetByTicket(int idTicket)
        {
            var item = await _context.InboundEmails.AsNoTracking()
                .Where(x => x.IdTicket == idTicket)
                .OrderBy(x => x.ReceivedAt)
                .FirstOrDefaultAsync();

            return Ok(item);
        }

        [HttpGet("{id}/attachments")]
        public async Task<ActionResult<List<InboundEmailAttachmentInfo>>> GetAttachments(int id)
        {
            var items = await _context.InboundEmailAttachments.AsNoTracking()
                .Where(a => a.IdInboundEmail == id)
                .Select(a => new InboundEmailAttachmentInfo { Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, Size = a.Size })
                .ToListAsync();

            return Ok(items);
        }

        // POST: api/InboundEmails/5/create-ticket?idType=&idCompany= -> crea un ticket dall'email ricevuta.
        // Owner = utente corrente. idCompany default = azienda dell'email; idType default = primo tipo disponibile.
        [HttpPost("{id}/create-ticket")]
        public async Task<ActionResult<int>> CreateTicket(int id, [FromQuery] int? idType = null, [FromQuery] int? idCompany = null)
        {
            var inbound = await _context.InboundEmails.FindAsync(id);
            if (inbound == null)
                return NotFound();

            if (inbound.IdTicket != null)
                return BadRequest("Ticket già collegato a questa email.");

            var companyId = idCompany ?? inbound.IdCompany;
            if (companyId == null)
                return BadRequest("Nessuna azienda: associa prima l'email a un'azienda.");

            var typeId = idType ?? await _context.TicketTypes.OrderBy(t => t.Id).Select(t => (int?)t.Id).FirstOrDefaultAsync();
            if (typeId == null)
                return BadRequest("Nessun tipo di ticket configurato.");

            var idUser = await _permitsService.IdUser();
            var now = System.DateTime.Now;

            var ticket = new Ticket
            {
                IdCompany = companyId.Value,
                IdType = typeId.Value,
                IdUserOpened = idUser,
                IdContact = inbound.IdContact,
                Description = string.IsNullOrWhiteSpace(inbound.Body) ? (inbound.Subject ?? "Email ricevuta") : $"{inbound.Subject}\n\n{inbound.Body}",
                DateOpened = now,
                Date = now,
                Numero = string.Empty,
                CloseDescription = string.Empty,
                CloseNote = string.Empty
            };
            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            inbound.IdTicket = ticket.Id;
            inbound.IdCompany = companyId;
            inbound.IsMatched = true;
            inbound.Handled = true;
            await _context.SaveChangesAsync();

            return Ok(ticket.Id);
        }

        [HttpGet("attachments/{attachmentId}/download")]
        public async Task<IActionResult> DownloadAttachment(int attachmentId)
        {
            var att = await _context.InboundEmailAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId);
            if (att == null) return NotFound();

            return File(att.Content, att.ContentType ?? "application/octet-stream", att.FileName);
        }

        // POST: api/InboundEmails/5/associate?idCompany=12 -> associa manualmente il messaggio a un'azienda
        // e crea l'attività email nella sua timeline.
        [HttpPost("{id}/associate")]
        public async Task<IActionResult> Associate(int id, [FromQuery] int idCompany)
        {
            var inbound = await _context.InboundEmails.FindAsync(id);
            if (inbound == null)
                return NotFound();

            if (!await _context.Companies.AnyAsync(c => c.Id == idCompany))
                return BadRequest("Azienda inesistente.");

            var now = System.DateTime.Now;

            var activity = new Activity
            {
                Kind = ActivityKind.Email,
                Subject = string.IsNullOrWhiteSpace(inbound.Subject) ? "Email ricevuta" : inbound.Subject!,
                Description = inbound.Body,
                EntityType = ActivityEntityType.Company,
                EntityId = idCompany,
                State = ActivityState.Done,
                DoneDate = inbound.ReceivedAt,
                CreatedAt = now
            };
            _context.Activities.Add(activity);

            inbound.IdCompany = idCompany;
            inbound.IsMatched = true;
            inbound.Handled = true;

            await _context.SaveChangesAsync();

            inbound.IdActivity = activity.Id;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
