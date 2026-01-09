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
using Microsoft.AspNetCore.Authorization;
using CRM.Client.Services;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AccessoryTypeLanguagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        public AccessoryTypeLanguagesController(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService;
        }

        [HttpGet]
        public async Task<IEnumerable<AccessoryTypeLanguage>?> GetAccessoryTypeLangs([FromQuery] AccessoryTypeLanguageFilter? args = null)
        {
            try
            {
                var items = _context.AccessoryTypeLanguages.Include(x=>x.Language).AsQueryable();

                if (args?.IdAccessoryType != null)
                {
                    items = items.Where(x => x.IdAccessoryType == args.IdAccessoryType);
                }

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderBy(x => x.Language.Name);

               

                int count = items.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }
                var paginationMetadata = new
                {
                    totalCount = count,
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

                return await items.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AccessoryTypeLanguagesController), nameof(GetAccessoryTypeLangs), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }



        // GET: api/Projects/5
        [HttpGet("{id}")]
        public async Task<ActionResult<AccessoryTypeLanguage>> GetAccessoryTypeLang(int id)
        {
            var item = await _context.AccessoryTypeLanguages.FindAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            return item;
        }

        // PUT: api/InterventionType/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAccessoryTypeLang(int id, [FromBody] AccessoryTypeLanguage item)
        {

            if (id != item.Id)
            {
                return BadRequest();
            }
            _context.Entry(item).State = EntityState.Modified;

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
        public async Task<ActionResult<InterventionTypeLanguage?>> PostAccessoryTypeLang([FromBody] AccessoryTypeLanguage item)
        {
            try
            {
                _context.AccessoryTypeLanguages.Add(item);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetAccessoryTypeLang", new { id = item.Id }, item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AccessoryTypeLanguagesController), nameof(PostAccessoryTypeLang), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

        // DELETE: api/Projects/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicketType(int id)
        {
            var item = await _context.AccessoryTypeLanguages.FindAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            _context.AccessoryTypeLanguages.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        private bool AccessoryTypeLangExists(int id)
        {
            return _context.AccessoryTypeLanguages.Any(e => e.Id == id);
        }
    }
}
