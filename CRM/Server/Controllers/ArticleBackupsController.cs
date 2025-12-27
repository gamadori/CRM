using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using CRM.Server.Services;
using CRM.Shared.Helper;
using CRM.Server.Helpers;
using Microsoft.Extensions.Primitives;
using CNM.Authorize;
using System.Drawing.Printing;
using CRM.Client.Services;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ArticleBackupsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        private readonly ILanguagesService _languagesService;
        public ArticleBackupsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPermitsService permitsService, ILogEventService logEventService, ILanguagesService languagesService)
        {
            _context = context;
            _userManager = userManager;
            _permitsService = permitsService;
            _logEventService = logEventService;
            _languagesService = languagesService;
        }
      
      
        // GET: api/Products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ArticleBackup>>?> Get([FromQuery] ArticleBackupFilter args)
        {
            try
            {
                

                if (!await _permitsService.ArticleCanAccess(args.IdArticle))
                {
                    return Problem("No access to article");
                }

                var items = _context.ArticleBackups.Where(x=>x.IdArticle == args.IdArticle).AsQueryable();


                if (args.TimeStampFrom != null)
                {
                    items = items.Where(x => x.TimeStamp >= args.TimeStampFrom);
                }

                if (args.TimeStampTo != null)
                {
                    items = items.Where(x => x.TimeStamp <= args.TimeStampTo);
                }

                if (args?.Filter != null && args.Filter.Trim().Length > 0)
                {
                    items = items.Where(args.Filter);
                }

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderBy(x => x.TimeStamp).ThenBy(x=>x.Description);

                int count = items.Count();
                int totalPage = 0;

                if (args?.Skip != null && args?.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }
                
                else
                {
                    totalPage = 1;

                }
               
                var paginationMetadata = new
                {
                    totalCount = count,
                    pageSize = args.PageSize,
                    currentPage = args.PageNumber,
                    totalPage = totalPage,
                   
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

                return await items.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticleBackupsController), nameof(Get), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }
       

        // GET: api/articles/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ArticleBackup>> Get(int id)
        {
            var item = await _context.ArticleBackups.FindAsync(id);

            if (item == null) 
            {
                return NotFound();
            }

            var resp = await _permitsService.CompanyCanAccess(item.Article.IdCompany);

            if (resp.CanAccess)
            {

                if (resp.IdCompany != null && item?.Article?.IdCompany != resp.IdCompany)
                {
                    item = null;
                }
            }
            if (item == null)
            {
                return NotFound();
            }

             
            
            return item;
        }

        // PUT: api/articles/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.SuperUserRole)]
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, ArticleBackup item)
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

        // POST: api/Products
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.SuperUserRole)]
        [HttpPost]
        public async Task<ActionResult<ArticleBackup>?> Post(ArticleBackup item)
        {
            try
            {
                _context.ArticleBackups.Add(item);
                await _context.SaveChangesAsync();

                return CreatedAtAction("Get", new { id = item.Id }, item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticleBackupsController), nameof(Post), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

        // DELETE: api/Products/5
        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.ArticleBackups.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            _context.ArticleBackups.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        

        private bool ItemExists(int id)
        {
            return _context.ArticleBackups.Any(e => e.Id == id);
        }

       


    }
}
