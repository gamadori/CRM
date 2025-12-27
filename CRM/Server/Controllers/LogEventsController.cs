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
using static CRM.Shared.LogEvent;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogEventsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LogEventsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/ProjectModels
        [HttpGet]
        public ActionResult<object> GetLogEvent()
        {
            try
            {
                string filter = null;
                string order;

                var data = _context.LogEvents.OrderByDescending(x=>x.DateEvent).AsQueryable();

                var count = data.Count();
                var queryString = Request.Query;


                if (queryString.Keys.Contains("$filter"))
                {
                    filter = queryString["$filter"];

                    data = SyncHelper.GetFilterPredicate(data, filter);


                }

                if (queryString.Keys.Contains("$orderby"))
                {
                    order = queryString["$orderby"];
                    data = data.OrderBy(order);

                }

                if (queryString.Keys.Contains("$inlinecount"))
                {

                    StringValues Skip;
                    StringValues Take;
                    int skip = (queryString.TryGetValue("$skip", out Skip)) ? Convert.ToInt32(Skip[0]) : 0;
                    int top = (queryString.TryGetValue("$top", out Take)) ? Convert.ToInt32(Take[0]) : data.Count();



                    return new { Items = data.Skip(skip).Take(top), Count = count };
                }
                else
                {

                    return data.ToList();
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        // GET: api/Projects/5
        [HttpGet("{id}")]
        public async Task<ActionResult<LogEvent>> GetLogEvent(int id)
        {
            var logEvent = await _context.LogEvents.FindAsync(id);

            if (logEvent == null)
            {
                return NotFound();
            }

            return logEvent;
        }

        // PUT: api/Projects/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutLogEvent(int id, LogEvent logEvent)
        {
            if (id != logEvent.Id)
            {
                return BadRequest();
            }

            _context.Entry(logEvent).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LogEventExists(id))
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

        // POST: api/Projects
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Project>> PostProject(LogEvent logEvent)
        {
            _context.LogEvents.Add(logEvent);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetLogEvent", new { id = logEvent.Id }, logEvent);
        }

        // DELETE: api/Projects/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLogEvent(int id)
        {
            var logEvent = await _context.LogEvents.FindAsync(id);
            if (logEvent == null)
            {
                return NotFound();
            }

            _context.LogEvents.Remove(logEvent);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        // Esempio in un controller API
        [HttpGet("activities")]
        public async Task<IActionResult> GetActivities([FromQuery] string? userId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] ActivityType? type)
        {
            var query = _context.LogEvents.AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(x => x.UserId == userId);

            if (fromDate.HasValue)
                query = query.Where(x => x.DateEvent >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(x => x.DateEvent <= toDate.Value);

            

            var activities = await query.Select(x=>new ActivityModel() { Date = x.DateEvent, Description = x.Message, Title =x.Module, Type = x.ActivityType }).OrderByDescending(x => x.Date).Take(20).ToListAsync();
            return Ok(activities);
        }
        private bool LogEventExists(int id)
        {
            return _context.LogEvents.Any(e => e.Id == id);
        }

        
    }
}
