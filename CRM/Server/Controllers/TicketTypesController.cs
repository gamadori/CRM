using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using System.Linq.Dynamic.Core;
using Newtonsoft.Json;
using CRM.Server.Services;
using Microsoft.AspNetCore.Authorization;
using CRM.Client.Services;
using AutoMapper.Internal;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TicketTypesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly IPermitsService _permits;

        private readonly ILogEventService _logEventService;

        private readonly ILanguagesService _languageService;

        public TicketTypesController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permitsService, ILanguagesService languagesService )
        {
            _context = context;
            _logEventService = logEventService;
            _permits = permitsService;
            _languageService = languagesService;
        }
      
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TicketType>>> GetTicketTypes([FromQuery] TicketTypeFilter args)
        {
            try
            {
                int totalPage = 1;

                var items = _context.TicketTypes.AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderBy(x => x.Desc);

                if (args?.Filter != null && args.Filter.Any())
                {
                    items = items.Where(args.Filter);
                }

                if (await _permits.IsClient())
                {
                    items = items.Where(x => x.CustomerEnabled == true);
                }

                int count = items.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }
                else
                {
                    totalPage = 1;

                }
               


                bool nextPage = args?.PageNumber < totalPage;
                bool previousPage = args?.PageNumber > 1;

                var paginationMetadata = new
                {
                    totalCount = count,
                    pageSize = args != null ? args.PageSize : 0,
                    currentPage = args != null ? args.PageNumber : 0,
                    totalPage = totalPage,
                    previousPage = previousPage,
                    nextPage = nextPage
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));
                // var list = await companies.ToListAsync();
                var list = await items.ToListAsync();

                var idLanguage = await _languageService.GetIdLanguage();

                foreach (var item in list)
                {
                    item.Language = _context.TicketTypesLanguages.FirstOrDefault(x => x.IdLanguage == idLanguage && x.IdTicketType == item.Id)?.Name ??
                        item.Desc;

                }
                return list;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketTypesController), nameof(GetTicketTypes), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // GET: api/TicketTypes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TicketType>> GetTicketType(int id)
        {
            var idLanguage = await _languageService.GetIdLanguage();
            var ticketType = await _context.TicketTypes.FindAsync(id);

            if (ticketType == null)
            {
                return new TicketType();
            }
            var translate = await _context.TicketTypesLanguages.FirstOrDefaultAsync(x => x.IdTicketType == ticketType.Id && x.IdLanguage == idLanguage);
            ticketType.Language = translate?.Name ?? ticketType.Desc;
            return ticketType;
        }

        // PUT: api/TicketTypes/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTicketType(int id, TicketType ticketType)
        {
            if (id != ticketType.Id)
            {
                return BadRequest();
            }

            _context.Entry(ticketType).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TicketTypeExists(id))
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

        // POST: api/TicketTypes
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TicketType>> PostTicketType(TicketType ticketType)
        {
            _context.TicketTypes.Add(ticketType);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTicketType", new { id = ticketType.Id }, ticketType);
        }

        // DELETE: api/TicketTypes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicketType(int id)
        {
            var ticketType = await _context.TicketTypes.FindAsync(id);
            if (ticketType == null)
            {
                return NotFound();
            }

            _context.TicketTypes.Remove(ticketType);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TicketTypeExists(int id)
        {
            return _context.TicketTypes.Any(e => e.Id == id);
        }
    }
}
