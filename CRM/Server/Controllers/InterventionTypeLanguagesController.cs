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
    public class InterventionTypeLanguagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        public InterventionTypeLanguagesController(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService;
        }

        [HttpGet]
        public async Task<IEnumerable<InterventionTypeLanguage>?> GetInterventionTypeLangs([FromQuery] InterventionTypeLangFilter? args = null)
        {
            try
            {
                var interventions = _context.InterventionTypeLanguages.Include(x=>x.Language).AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    interventions = interventions.OrderBy(args.OrderBy);
                }
                else
                    interventions = interventions.OrderBy(x => x.IdInterventionType);

                if (args != null && args.IdInterventionType != null)
                    interventions = interventions.Where(x=>x.IdInterventionType == args.IdInterventionType);

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
                await _logEventService.RegisterAsync(nameof(InterventionTypeLanguagesController), nameof(GetInterventionTypeLangs), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }



        // GET: api/Projects/5
        [HttpGet("{id}")]
        public async Task<ActionResult<InterventionTypeLanguage>> GetInterventionType(int id)
        {
            var item = await _context.InterventionTypeLanguages.FindAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            return item;
        }

        // PUT: api/InterventionType/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutInterventionType(int id, [FromBody] InterventionTypeLanguage intervention)
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
        public async Task<ActionResult<InterventionTypeLanguage?>> PostInterventionTypet([FromBody] InterventionTypeLanguage interventionType)
        {
            try
            {
                _context.InterventionTypeLanguages.Add(interventionType);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetInterventionTypeLangs", new { id = interventionType.Id }, interventionType);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InterventionTypeLanguagesController), nameof(PostInterventionTypet), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

        // DELETE: api/Projects/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInterventionType(int id)
        {
            var interventionType = await _context.InterventionTypeLanguages.FindAsync(id);
            if (interventionType == null)
            {
                return NotFound();
            }

            _context.InterventionTypeLanguages.Remove(interventionType);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        private bool InterventionTypeExists(int id)
        {
            return _context.InterventionTypeLanguages.Any(e => e.Id == id);
        }
    }
}
