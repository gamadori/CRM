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
    public class TicketTypeGroupsController : ControllerBase
    {


        private readonly ApplicationDbContext _context;
        public TicketTypeGroupsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // PUT: api/Customers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ApiResponseModel>> PostGroup(TicketTypeGroup model)
        {
            

            var ticketType = await _context.TicketTypes.Where(x => x.Id == model.IdTicket).Include(x=>x.Groups).SingleOrDefaultAsync();
            var group = await _context.Groups.Where(x => x.Id == model.IdGroup).SingleOrDefaultAsync();

            if (ticketType == null)
                return new ApiResponseModel() { State = false, Message = "Ticket Type insesistente" };


            if (group == null)
                return new ApiResponseModel() { State = false, Message = "Gruppo insesistente" };

            try
            {
                ticketType.Groups.Add(group);
                await _context.SaveChangesAsync();

                
            }
            catch (DbUpdateConcurrencyException ex)
            {
                    return new ApiResponseModel() { State = false, Message = ex.Message };
                
            }

            return new ApiResponseModel() { State = true };
        }
        [HttpDelete]
        public async Task<IActionResult> Delete([FromQuery] TicketTypeGroup model)
        {
            var ticketType = await _context.TicketTypes.Include(x=>x.Groups).Where(x=>x.Id == model.IdTicket).FirstOrDefaultAsync();
            if (ticketType == null)
            {
                return NotFound();
            }

            var group = await _context.Groups.FindAsync(model.IdGroup);

            ticketType.Groups.Remove(group);
            await _context.SaveChangesAsync();

            return NoContent();
        }

       
    }
}
