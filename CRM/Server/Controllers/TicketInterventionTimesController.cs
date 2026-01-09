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
using CRM.Client.Pages.DashBoard;
using Newtonsoft.Json;
using CRM.Server.Services;
using CRM.Client.Services;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketInterventionTimesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        private readonly IPermitsService _permits;

        public TicketInterventionTimesController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permits)
        {
            _context = context;
            _logEventService = logEventService;
            _permits = permits;
            
        }

        // GET: api/Deals
        [HttpGet]
        public async Task<ActionResult<List<TicketInterventionTimeModel>>> Get([FromQuery] TicketInterventionTimeFilter args)
        {
            try
            {


                IQueryable<TicketInterventionTime> items = _context.TicketInterventionTimes.Where(x => x.IdTicketIntervention == args.IdTicketIntervention).OrderBy(x => x.StartDateTime);
                   

                if (!await _permits.CanAccessOtherCompany())
                {
                    int? idCompany = await _permits.GetIdCompany();
                    items = items.Where(x => x.TicketIntervention.Ticket.IdCompany == idCompany);

                }
                var itemsModel = items.Select(x => new TicketInterventionTimeModel() { Id = x.Id, IdTicketIntervention = x.IdTicketIntervention, 
                    StartDateTime = x.StartDateTime, EndDateTime = x.EndDateTime });
                return await itemsModel.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AccessoriesController), nameof(Get), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // GET: api/Deals/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TicketInterventionTimeModel>> Get(int id)
        {
            
            var time = await _context.TicketInterventionTimes.FindAsync(id);

           
               
          

            if (time == null)
            {
                return NotFound();
            }
            else if (!await _permits.CanGetObject(time.TicketIntervention?.Ticket?.IdCompany))
            {

                return BadRequest();
            }

           

            return time.ToModel();
           
        }

        

        // PUT: api/Deals/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, TicketInterventionTime item)
        {
            try
            {
                if (id != item.Id)
                {
                    return BadRequest();
                }

                if (!await _permits.CanEditTicket())
                    return Problem(GlobalMessages.PermitsErrors);

                
                _context.Entry(item).State = EntityState.Modified;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionTimesController), nameof(Put), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TimeExists(id))
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

        // POST: api/Deals
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Deal>> Post(TicketInterventionTime item)
        {
            if (await _permits.CanEditTicket())
            {
                
                _context.TicketInterventionTimes.Add(item);
                await _context.SaveChangesAsync();

                return CreatedAtAction("Get", new { id = item.Id }, item);
            }
            else
                return Problem(GlobalMessages.PermitsErrors);
        }

        // DELETE: api/Deals/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccessory(int id)
        {
            if (await _permits.CanEditTicket())
            {
                var item = await _context.TicketInterventionTimes.FindAsync(id);
                if (item == null)
                    return NotFound();
                else
                {
                    _context.TicketInterventionTimes.Remove(item);
                    await _context.SaveChangesAsync();

                    return NoContent();
                }
            }
            else
                return Problem(GlobalMessages.PermitsErrors);
        }

        private bool TimeExists(int id)
        {
            return _context.TicketInterventionTimes.Any(e => e.Id == id);
        }
    }
}
