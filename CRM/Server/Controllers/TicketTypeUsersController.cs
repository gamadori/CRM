using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Math.EC.Rfc7748;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketTypeUsersController : ControllerBase
    {


        private readonly ApplicationDbContext _context;
        public TicketTypeUsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // PUT: api/Customers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ApiResponseModel>> PostUser(TicketTypeUser model)
        {
            

            var ticketType = await _context.TicketTypes.Where(x => x.Id == model.IdTicket).Include(x=>x.Users).SingleOrDefaultAsync();
            var user = await _context.Users.Where(x => x.Id == model.IdUser).SingleOrDefaultAsync();

            if (ticketType == null)
                return new ApiResponseModel() { State = false, Message = "Tipo di Ticket inesitente" };


            if (user == null)
                return new ApiResponseModel() { State = false, Message = "Utente inesistente" };

            try
            {
                ticketType.Users.Add(user);
                await _context.SaveChangesAsync();

                
            }
            catch (DbUpdateConcurrencyException ex)
            {
                    return new ApiResponseModel() { State = false, Message = ex.Message };
                
            }

            return new ApiResponseModel() { State = true };
        }
        [HttpDelete]
        public async Task<IActionResult> Delete([FromQuery] TicketTypeUser model)
        {
            var group = await _context.TicketTypes.Include(x=>x.Users).Where(x=>x.Id == model.IdTicket).FirstOrDefaultAsync();
            if (group == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(model.IdUser);

            group.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

       
    }
}
