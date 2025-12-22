using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketStatesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public TicketStatesController(ApplicationDbContext context)
        {
            _context = context;

           
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TicketState>>> GetTicketState([FromQuery] TicketStateFilter args)
        {
            int totalPage;

            var ticketStates = _context.TicketStates.OrderBy(x => x.State).AsQueryable();

           
            if (args.Description?.Length > 0)
            {
                ticketStates = ticketStates.Where(x => x.Description.Contains(args.Description));
            }

            int count = ticketStates.Count();

            if (args.PageSize > 0)
            {
                ticketStates = ticketStates.Skip((args.PageNumber - 1) * args.PageSize).Take(args.PageSize);
                totalPage = (int)Math.Ceiling(count / (double)args.PageSize);
            }
            else
            {
                totalPage = 1;

            }
            bool nextPage = args.PageNumber < totalPage;
            bool previousPage = args.PageNumber > 1;

            var paginationMetadata = new
            {
                totalCunt = count,
                pageSize = args.PageSize,
                currentPage = args.PageNumber,
                totalPage = totalPage,
                previousPage = previousPage,
                nextPage = nextPage
            };
            HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

            return await ticketStates.ToListAsync();
        }
        // GET: api/TicketStates/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TicketState>> GetTicketState(int id)
        {
            var ticketState = await _context.TicketStates.FindAsync(id);

            if (ticketState == null)
            {
                return NotFound();
            }

            return ticketState;
        }

        // PUT: api/TicketStates/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTicketState(int id, TicketState ticketState)
        {
            if (id != ticketState.Id)
            {
                return BadRequest();
            }

            _context.Entry(ticketState).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TicketStateExists(id))
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

        // POST: api/TicketStates
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TicketState>> PostTicketState(TicketState ticketState)
        {
            _context.TicketStates.Add(ticketState);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTicketState", new { id = ticketState.Id }, ticketState);
        }

        // DELETE: api/TicketStates/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicketState(int id)
        {
            var ticketState = await _context.TicketStates.FindAsync(id);
            if (ticketState == null)
            {
                return NotFound();
            }

            _context.TicketStates.Remove(ticketState);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TicketStateExists(int id)
        {
            return _context.TicketStates.Any(e => e.Id == id);
        }
    }
}
