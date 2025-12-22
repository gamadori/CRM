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
    public class SmtpSettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SmtpSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/SmtpSettings
        [HttpGet]
        public async Task<ActionResult<SmtpSettings?>> Get()
        {
            return await _context.SmtpSettings.FirstOrDefaultAsync();
        }

        // GET: api/SmtpSettings/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SmtpSettings>> Get(int id)
        {
            var smtpSettings = await _context.SmtpSettings.FindAsync(id);

            if (smtpSettings == null)
            {
                return NotFound();
            }

            return smtpSettings;
        }

        // PUT: api/SmtpSettings/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSmtpSettings(int id, SmtpSettings smtpSettings)
        {
            if (id != smtpSettings.Id)
            {
                return BadRequest();
            }

            _context.Entry(smtpSettings).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SmtpSettingsExists(id))
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
        public async Task<ActionResult<SmtpSettings>> PostSmtpSettings(SmtpSettings smtpSettings)
        {
            _context.SmtpSettings.Add(smtpSettings);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSmtpSettings", new { id = smtpSettings.Id }, smtpSettings);
        }

        // DELETE: api/SmtpSettings/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSmtpSettings(int id)
        {
            var smtpSettings = await _context.SmtpSettings.FindAsync(id);
            if (smtpSettings == null)
            {
                return NotFound();
            }

            _context.SmtpSettings.Remove(smtpSettings);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool SmtpSettingsExists(int id)
        {
            return _context.SmtpSettings.Any(e => e.Id == id);
        }
    }
}
