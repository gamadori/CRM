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

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LanguagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        public LanguagesController(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService;
        }

        // GET: api/
        [HttpGet]
        public ActionResult<object> GetLanguages()
        {
            try
            {
                string? filter = null;
                string order;

                var data = _context.Languages.AsQueryable();

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

                    IQueryable<Language> items;

                    if (top == 0)
                    {
                        items = data;

                    }
                    else
                        items = data.Skip(skip).Take(top);
                    return new { Items = items, Count = count };
                }
                else
                {

                    return data.ToList();
                }
            }
            catch (Exception ex)
            {

                _logEventService.Register(nameof(LanguagesController), nameof(GetLanguages), LogEvent.EventsTypes.Error, ex.Message);

                return new { Items = new List<Language>(), Count = 0 };
            }
        }


        // GET: api/Projects/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Language>> GetLanguage(int id)
        {
            var language = await _context.Languages.FindAsync(id);

            if (language == null)
            {
                return NotFound();
            }

            return language;
        }

        // PUT: api/InterventionType/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutLanguage(int id, Language language)
        {
           

            _context.Entry(language).State = EntityState.Modified;

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
        public async Task<ActionResult<Language>> PostLanguage([FromBody] Language language)
        {
            try
            {
                _context.Languages.Add(language);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetLanguage", new { id = language.Id }, language);
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        // DELETE: api/Projects/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLanguage(int id)
        {
            var language = await _context.Languages.FindAsync(id);
            if (language == null)
            {
                return NotFound();
            }

            _context.Languages.Remove(language);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        
       

        
    }
}
