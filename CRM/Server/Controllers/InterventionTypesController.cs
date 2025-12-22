using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.Resources.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Linq.Dynamic.Core;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InterventionTypesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        public InterventionTypesController(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService;
        }

        [HttpGet]
        public async Task<IEnumerable<InterventionType>?> GetInterventionTypes([FromQuery] InterventionTypeFilter? args = null)
        {
            try
            {


                var interventions = _context.InterventionTypes.AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    interventions = interventions.OrderBy(args.OrderBy);
                }
                else
                    interventions = interventions.OrderBy(x => x.Name);

               
                int count = interventions.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    interventions = interventions.Skip(args.Skip.Value).Take(args.Top.Value);
                }

                var paginationMetadata = new
                {
                    totalCount = count,
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));
                // var list = await companies.ToListAsync();
                return await interventions.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InterventionTypesController), nameof(GetInterventionTypes), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }
       


        // GET: api/Projects/5
        [HttpGet("{id}")]
        public async Task<ActionResult<InterventionType>> GetInterventionType(int id)
        {
            var project = await _context.InterventionTypes.FindAsync(id);

            if (project == null)
            {
                return NotFound();
            }

            return project;
        }

        // PUT: api/InterventionType/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutInterventionType(int id, [FromBody] InterventionType intervention)
        {

            if (id != intervention.Id)
            {
                return BadRequest();
            }
            _context.Entry(intervention).State = EntityState.Modified;

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
        public async Task<ActionResult<InterventionType?>> PostInterventionTypet([FromBody] InterventionType interventionType)
        {
            try
            {
                _context.InterventionTypes.Add(interventionType);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetInterventionType", new { id = interventionType.Id }, interventionType);
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesController), nameof(PostInterventionTypet), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

        // DELETE: api/Projects/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProject(int id)
        {
            var interventionType = await _context.InterventionTypes.FindAsync(id);
            if (interventionType == null)
            {
                return NotFound();
            }

            _context.InterventionTypes.Remove(interventionType);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        
        private bool InterventionTypeExists(int id)
        {
            return _context.InterventionTypes.Any(e => e.Id == id);
        }

        
    }
}
