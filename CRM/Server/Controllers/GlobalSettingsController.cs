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
    public class GlobalSettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public GlobalSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/GlobalSettings
        [HttpGet]
        public async Task<ActionResult<GlobalSetting>> Get()
        {
            return (await _context.GlobalSettings.FirstOrDefaultAsync()) ??  new GlobalSetting();
        }

        // GET: api/GlobalSettings/5
        [HttpGet("{id}")]
        public async Task<ActionResult<GlobalSetting>> Get(int id)
        {
            var settings = await _context.GlobalSettings.FindAsync(id);

            if (settings == null)
            {
                return NotFound();
            }

            return settings;
        }

        // PUT: api/SmtpSettings/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, GlobalSetting settings)
        {
            if (id != settings.Id)
            {
                return BadRequest();
            }

            _context.Entry(settings).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!GlobalSettingsExists(id))
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

        // POST: api/SmtpSettings
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<SmtpSettings>> Post(GlobalSetting settings)
        {
            _context.GlobalSettings.Add(settings);
            await _context.SaveChangesAsync();

            return CreatedAtAction("Get", new { id = settings.Id }, settings);
        }

        // DELETE: api/SmtpSettings/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var settings = await _context.GlobalSettings.FindAsync(id);
            if (settings == null)
            {
                return NotFound();
            }

            _context.GlobalSettings.Remove(settings);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool GlobalSettingsExists(int id)
        {
            return _context.GlobalSettings.Any(e => e.Id == id);
        }
    }
}
