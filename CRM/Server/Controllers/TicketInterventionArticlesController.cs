using CRM.Client.Services;
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
    public class TicketInterventionArticlesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        public TicketInterventionArticlesController(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService;
        }

        [HttpGet]
        public async Task<IEnumerable<TicketInterventionArticle>?> GetArticles([FromQuery] InterventionTypeFilter? args = null)
        {
            try
            {


                var articles = _context.TicketInterventionArticles.AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    articles = articles.OrderBy(args.OrderBy);
                }
                else
                    articles = articles.OrderBy(x => x.Id);

               
                int count = articles.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    articles = articles.Skip(args.Skip.Value).Take(args.Top.Value);
                }

                var paginationMetadata = new
                {
                    totalCount = count,
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));
                // var list = await companies.ToListAsync();
                return await articles.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionArticle), nameof(GetArticles), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }
       


        // GET: api/Projects/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TicketInterventionArticle>> GetArticle(int id)
        {
            var article = await _context.TicketInterventionArticles.FindAsync(id);

            if (article == null)
            {
                return NotFound();
            }

            return article;
        }

        // PUT: api/InterventionType/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutArticle(int id, [FromBody] TicketInterventionArticle article)
        {

            if (id != article.Id)
            {
                return BadRequest();
            }
            _context.Entry(article).State = EntityState.Modified;

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
        public async Task<ActionResult<TicketInterventionArticle?>> PostArticle([FromBody] TicketInterventionArticle article)
        {
            try
            {
                _context.TicketInterventionArticles.Add(article);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetInterventionType", new { id = article.Id }, article);
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionArticlesController), nameof(PostArticle), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        // DELETE: api/Projects/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletArticle(int id)
        {
            var article = await _context.TicketInterventionArticles.FindAsync(id);
            if (article == null)
            {
                return NotFound();
            }

            _context.TicketInterventionArticles.Remove(article);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        
        private bool ArticleExist(int id)
        {
            return _context.TicketInterventionArticles.Any(e => e.Id == id);
        }

        
    }
}
