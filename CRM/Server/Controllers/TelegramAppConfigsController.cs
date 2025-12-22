using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Server.Services;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TelegramAppConfigsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly WTelegramService WT;
        
        public TelegramAppConfigsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/SmtpSettings
        [HttpGet]
        public async Task<ActionResult<TelegramAppConfig?>> Get()
        {
            return await _context.TelegramAppConfigs.FirstOrDefaultAsync();
        }

        // GET: api/SmtpSettings/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TelegramAppConfig>> Get(int id)
        {
            var item = await _context.TelegramAppConfigs.FindAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            return item;
        }

        // PUT: api/SmtpSettings/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutItems(int id, TelegramAppConfig config)
        {
            if (id != config.Id)
            {
                return BadRequest();
            }

            _context.Entry(config).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ItemExists(id))
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
        public async Task<ActionResult<TelegramAppConfig>> PostItem(TelegramAppConfig item)
        {
            _context.TelegramAppConfigs.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction("Get", new { id = item.Id }, item);
        }

        // DELETE: api/SmtpSettings/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await _context.TelegramAppConfigs.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            _context.TelegramAppConfigs.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        
        private bool ItemExists(int id)
        {
            return _context.TelegramAppConfigs.Any(e => e.Id == id);
        }
    }
}
