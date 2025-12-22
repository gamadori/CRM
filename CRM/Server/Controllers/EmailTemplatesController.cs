using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using CNM.Authorize;
using CRM.Shared.Resources.Models;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class EmailTemplatesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EmailTemplatesController(ApplicationDbContext context)
        {
            _context = context;
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

        private bool EmailTemplateExists(int id)
        {
            return _context.EmailTemplates.Any(e => e.Id == id);
        }

        
    }
}
