using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Server.Services.Email;
using CRM.Shared;
using CRM.Shared.DTOs;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using CNM.Authorize;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class EmailTemplatesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSenderPlus _emailSender;
        private readonly IPermitsService _permitsService;

        private readonly IEmailTemplateTranslator _translator;

        public EmailTemplatesController(ApplicationDbContext context, IEmailSenderPlus emailSender, IPermitsService permitsService, IEmailTemplateTranslator translator)
        {
            _context = context;
            _emailSender = emailSender;
            _permitsService = permitsService;
            _translator = translator;
        }

        // GET: api/EmailTemplates
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EmailTemplate>>> GetEmailTemplate([FromQuery] EmailTemplateFilter args)
        {

            var emails = _context.EmailTemplates.OrderBy(x => x.Tipo).AsQueryable();

            if (args.Filter != null && args.Filter.Trim().Length > 0)
            {
                emails = emails.Where(args.Filter);
            }

            if (args.OrderBy != null && args.OrderBy.Length > 0)
            {
                emails = emails.OrderBy(args.OrderBy);
            }
            else
                emails = emails.OrderBy(x => x.Subject);


            int count = emails.Count();
            int totalPage = 0;

            if (args.Skip != null && args.Top != null)
            {
                emails = emails.Skip(args.Skip.Value).Take(args.Top.Value);
            }
            else
            {
                totalPage = 1;

            }

            var paginationMetadata = new
            {
                totalCount = count,
                totalPage = totalPage
            };
            HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

            return await emails.ToListAsync();
        }

        // GET: api/EmailTemplates/5
        [HttpGet("{id}")]
        public async Task<ActionResult<EmailTemplate>> GetEmailTemplate(int id)
        {
            var emailTemplate = await _context.EmailTemplates.FindAsync(id);

            if (emailTemplate == null)
            {
                return NotFound();
            }

            return emailTemplate;
        }

        // PUT: api/EmailTemplates/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.SuperUserRole)]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEmailTemplate(int id, EmailTemplate emailTemplate)
        {
            if (id != emailTemplate.Id)
            {
                return BadRequest();
            }

            _context.Entry(emailTemplate).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmailTemplateExists(id))
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

        // POST: api/EmailTemplates
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.SuperUserRole)]
        [HttpPost]
        public async Task<ActionResult<EmailTemplate>> PostEmailTemplate(EmailTemplate emailTemplate)
        {
            _context.EmailTemplates.Add(emailTemplate);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetEmailTemplate", new { id = emailTemplate.Id }, emailTemplate);
        }

        // DELETE: api/EmailTemplates/5
        [AuthorizeRole(ePolicy.SuperUserRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmailTemplate(int id)
        {
            var emailTemplate = await _context.EmailTemplates.FindAsync(id);
            if (emailTemplate == null)
            {
                return NotFound();
            }

            _context.EmailTemplates.Remove(emailTemplate);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/EmailTemplates/preview -> renderizza oggetto/corpo con valori di esempio (anteprima)
        [HttpPost("preview")]
        public ActionResult<object> Preview([FromBody] EmailTemplate template)
        {
            var values = SampleValues();
            return Ok(new
            {
                subject = EmailTemplateRenderer.Render(template.Subject, values),
                body = EmailTemplateRenderer.Render(template.Body, values)
            });
        }

        // POST: api/EmailTemplates/test -> invia una prova del template (valori di esempio) all'utente corrente
        [AuthorizeRole(ePolicy.SuperUserRole)]
        [HttpPost("test")]
        public async Task<IActionResult> Test([FromBody] EmailTemplate template)
        {
            var user = await _permitsService.GetUser();
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
                return BadRequest(new { Success = false, Message = "L'utente corrente non ha un'email." });

            var values = SampleValues();
            var subject = "[TEST] " + EmailTemplateRenderer.Render(template.Subject, values);
            var body = EmailTemplateRenderer.Render(template.Body, values);

            await _emailSender.SendEmailAsync(user.Email, subject, body);
            return Ok(new { Success = true, Message = $"Email di prova accodata a {user.Email}." });
        }

        // GET: api/EmailTemplates/by-type/3 -> il template come gruppo (tutte le lingue insieme)
        [HttpGet("by-type/{tipo}")]
        public async Task<ActionResult<EmailTemplateGroupDTO>> GetByType(EmailsTypes tipo)
        {
            var rows = await _context.EmailTemplates.Where(x => x.Tipo == tipo).ToListAsync();

            var group = new EmailTemplateGroupDTO
            {
                Tipo = tipo,
                IdLogo = rows.Select(r => r.IdLogo).FirstOrDefault(x => x != null),
                Versions = rows
                    .GroupBy(r => string.IsNullOrEmpty(r.Language) ? "it" : r.Language.ToLowerInvariant())
                    .Select(g => g.First())
                    .Select(r => new EmailTemplateVersionDTO
                    {
                        Id = r.Id,
                        Language = string.IsNullOrEmpty(r.Language) ? "it" : r.Language.ToLowerInvariant(),
                        Subject = r.Subject,
                        Body = r.Body
                    })
                    .ToList()
            };

            return Ok(group);
        }

        // POST: api/EmailTemplates/by-type -> salva tutte le lingue del template in un colpo.
        // Versioni con oggetto vuoto vengono rimosse; le altre create/aggiornate. Il logo è condiviso.
        [AuthorizeRole(ePolicy.SuperUserRole)]
        [HttpPost("by-type")]
        public async Task<IActionResult> SaveByType([FromBody] EmailTemplateGroupDTO group)
        {
            var existing = await _context.EmailTemplates.Where(x => x.Tipo == group.Tipo).ToListAsync();

            foreach (var version in group.Versions)
            {
                var lang = string.IsNullOrWhiteSpace(version.Language) ? "it" : version.Language.ToLowerInvariant();
                var row = existing.FirstOrDefault(e => (string.IsNullOrEmpty(e.Language) ? "it" : e.Language.ToLowerInvariant()) == lang);

                var hasContent = !string.IsNullOrWhiteSpace(version.Subject);

                if (hasContent)
                {
                    if (row == null)
                    {
                        _context.EmailTemplates.Add(new EmailTemplate
                        {
                            Tipo = group.Tipo,
                            Language = lang,
                            Subject = version.Subject!,
                            Body = version.Body ?? string.Empty,
                            IdLogo = group.IdLogo
                        });
                    }
                    else
                    {
                        row.Language = lang;
                        row.Subject = version.Subject!;
                        row.Body = version.Body ?? string.Empty;
                        row.IdLogo = group.IdLogo;
                    }
                }
                else if (row != null)
                {
                    _context.EmailTemplates.Remove(row); // versione svuotata → rimossa
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/EmailTemplates/translate -> traduce oggetto/corpo nelle lingue richieste (AI)
        [AuthorizeRole(ePolicy.SuperUserRole)]
        [HttpPost("translate")]
        public async Task<ActionResult<List<EmailTemplateVersionDTO>>> Translate([FromBody] EmailTemplateTranslateRequest request)
        {
            if (!_translator.IsAvailable)
                return BadRequest(new { Message = "Traduzione AI non configurata (manca Anthropic:ApiKey)." });

            var result = await _translator.TranslateAsync(request.SourceLanguage, request.Subject, request.Body, request.TargetLanguages);
            return Ok(result);
        }

        private static Dictionary<string, string> SampleValues() => new()
        {
            ["$NAME"] = "Mario Rossi",
            ["$COMPANY"] = "Acme S.r.l.",
            ["$TICKET"] = "1234",
            ["$URL"] = "https://esempio.crm/link",
            ["$DATE"] = DateTime.Now.ToString("g")
        };

        private bool EmailTemplateExists(int id)
        {
            return _context.EmailTemplates.Any(e => e.Id == id);
        }
    }
}
