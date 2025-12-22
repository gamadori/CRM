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
    public class TicketInterventionsTypesController : ControllerBase
    {

        private readonly ApplicationDbContext _context;

        public TicketInterventionsTypesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // PUT: api/Customers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ApiResponseModel>> PostInterventionType(TicketInterventionType model)
        {
            

            var intervention = await _context.TicketsInterventions.Where(x => x.Id == model.IdTicketIntervention).Include(x=>x.TicketInterventionsTypes).SingleOrDefaultAsync();
            var typeIntervention = await _context.InterventionTypes.Where(x => x.Id == model.IdInterventionType).SingleOrDefaultAsync();

            if (intervention == null)
                return new ApiResponseModel() { State = false, Message = "Intervento insesistente" };


            if (typeIntervention == null)
                return new ApiResponseModel() { State = false, Message = "Tipo intervento insesistente" };

            try
            {
                intervention.TicketInterventionsTypes.Add(typeIntervention);
                await _context.SaveChangesAsync();

                
            }
            catch (DbUpdateConcurrencyException ex)
            {
                    return new ApiResponseModel() { State = false, Message = ex.Message };
                
            }

            return new ApiResponseModel() { State = true };
        }
        [HttpDelete]
        public async Task<IActionResult> Delete([FromQuery] TicketInterventionType model)
        {
            var ticketIntervention = await _context.TicketsInterventions.Include(x=>x.TicketInterventionsTypes).Where(x=>x.Id == model.IdTicketIntervention).FirstOrDefaultAsync();
            if (ticketIntervention == null)
            {
                return NotFound();
            }

            var interventionType = await _context.InterventionTypes.FindAsync(model.IdInterventionType);

            ticketIntervention.TicketInterventionsTypes.Remove(interventionType);
            await _context.SaveChangesAsync();

            return NoContent();
        }

       
    }
}
