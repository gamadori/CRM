using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketInterventionUsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TicketInterventionUsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/TicketInterventionUsers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TicketInterventionUser>>> GetTicketInterventionUser()
        {
            return await _context.TicketInterventionUser.ToListAsync();
        }

        // GET: api/TicketInterventionUsers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TicketInterventionUser>> GetTicketInterventionUser(int id)
        {
            var ticketInterventionUser = await _context.TicketInterventionUser.FindAsync(id);

            if (ticketInterventionUser == null)
            {
                return NotFound();
            }

            return ticketInterventionUser;
        }

        // PUT: api/TicketInterventionUsers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTicketInterventionUser(int id, TicketInterventionUser ticketInterventionUser)
        {
            if (id != ticketInterventionUser.Id)
            {
                return BadRequest();
            }

            _context.Entry(ticketInterventionUser).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TicketInterventionUserExists(id))
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

      
        // POST: api/TicketInterventionUsers
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TicketInterventionUser>> PostTicketInterventionUser(TicketInterventionUser ticketInterventionUser)
        {
            var user = await _context.Users.Where(x => x.Id == ticketInterventionUser.IdUser).SingleOrDefaultAsync();
            var intervention = await _context.TicketsInterventions.Where(x => x.Id == ticketInterventionUser.IdIntervention).SingleOrDefaultAsync();

            if (user == null || intervention == null)
                return NotFound();

            _context.TicketInterventionUser.Add(ticketInterventionUser);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTicketInterventionUser", new { id = ticketInterventionUser.Id }, ticketInterventionUser);
        }


        // DELETE: api/TicketInterventionUsers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicketInterventionUser(int id)
        {
            var ticketInterventionUser = await _context.TicketInterventionUser.FindAsync(id);
            if (ticketInterventionUser == null)
            {
                return NotFound();
            }

            _context.TicketInterventionUser.Remove(ticketInterventionUser);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TicketInterventionUserExists(int id)
        {
            return _context.TicketInterventionUser.Any(e => e.Id == id);
        }
    }
}
