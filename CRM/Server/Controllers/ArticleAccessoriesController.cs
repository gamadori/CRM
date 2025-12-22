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
    public class ArticleAccessoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ArticleAccessoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/ArticleAccessories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ArticleAccessory>>> GetArticleAccessory()
        {
            return await _context.ArticleAccessory.ToListAsync();
        }

        // GET: api/ArticleAccessories/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ArticleAccessory>> GetArticleAccessory(int id)
        {
            var articleAccessory = await _context.ArticleAccessory.FindAsync(id);

            if (articleAccessory == null)
            {
                return NotFound();
            }

            return articleAccessory;
        }

        // PUT: api/ArticleAccessories/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutArticleAccessory(int id, ArticleAccessory articleAccessory)
        {
            if (id != articleAccessory.Id)
            {
                return BadRequest();
            }

            _context.Entry(articleAccessory).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ArticleAccessoryExists(id))
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

        // POST: api/ArticleAccessories
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ArticleAccessory>> PostArticleAccessory(ArticleAccessory articleAccessory)
        {
            _context.ArticleAccessory.Add(articleAccessory);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetArticleAccessory", new { id = articleAccessory.Id }, articleAccessory);
        }

        // DELETE: api/ArticleAccessories/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteArticleAccessory(int id)
        {
            var articleAccessory = await _context.ArticleAccessory.FindAsync(id);
            if (articleAccessory == null)
            {
                return NotFound();
            }

            _context.ArticleAccessory.Remove(articleAccessory);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ArticleAccessoryExists(int id)
        {
            return _context.ArticleAccessory.Any(e => e.Id == id);
        }
    }
}
