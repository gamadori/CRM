using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Server.Helpers;
using Microsoft.Extensions.Primitives;
using CRM.Shared.Helper;
using CRM.Server.Services;
using Newtonsoft.Json;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketTypesLanguagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        public TicketTypesLanguagesController(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService;
        }

        [HttpGet]
        public async Task<IEnumerable<TicketTypeLanguage>?> GetTicketTypeLangs([FromQuery] TicketTypeLanguageFilter? args = null)
        {
            try
            {
                var items = _context.TicketTypesLanguages.Include(x=>x.Language).AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderBy(x => x.IdTicketType);

                if (args != null && args.IdTicketType != null)
                    items = items.Where(x=>x.IdTicketType == args.IdTicketType);

                int count = items.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }
                var paginationMetadata = new
                {
                    totalCount = count,
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));
                // var list = await companies.ToListAsync();
                return await items.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketTypesLanguagesController), nameof(GetTicketTypeLangs), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }



        // GET: api/Projects/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TicketTypeLanguage>> GetTicketType(int id)
        {
            var item = await _context.TicketTypesLanguages.FindAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            return item;
        }

        // PUT: api/InterventionType/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTicketType(int id, [FromBody] TicketTypeLanguage item)
        {

            if (id != item.Id)
            {
                return BadRequest();
            }
            _context.Entry(item).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {

                return NotFound();

            }

            return NoContent();
        }

        // POST: api/Projects
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<InterventionTypeLanguage?>> PostTicketTypet([FromBody] TicketTypeLanguage ticketType)
        {
            try
            {
                _context.TicketTypesLanguages.Add(ticketType);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetTicketTypeLangs", new { id = ticketType.Id }, ticketType);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketTypesLanguagesController), nameof(PostTicketTypet), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

        // DELETE: api/Projects/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicketType(int id)
        {
            var ticketType = await _context.TicketTypesLanguages.FindAsync(id);
            if (ticketType == null)
            {
                return NotFound();
            }

            _context.TicketTypesLanguages.Remove(ticketType);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        private bool TicketTypeExists(int id)
        {
            return _context.TicketTypesLanguages.Any(e => e.Id == id);
        }
    }
}
