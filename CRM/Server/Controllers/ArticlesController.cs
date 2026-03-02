using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ArticlesController : ControllerBase
    {
        private readonly IArticlesService _articlesService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        private readonly ILanguagesService _languagesService;
        public ArticlesController(IArticlesService articlesService, UserManager<ApplicationUser> userManager, IPermitsService permitsService, ILogEventService logEventService, ILanguagesService languagesService)
        {
            _articlesService = articlesService;
            _userManager = userManager;
            _permitsService = permitsService;
            _logEventService = logEventService;
            _languagesService = languagesService;
        }


        [HttpGet]
        public async Task<PagingResponse<ArticleDTO>?> GetPage([FromQuery] ArticleFilter? args = null)
        {
            try
            {
                var items = await _articlesService.GetPagingAsync(args);
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }
        [HttpGet("list")]
        public async Task<IEnumerable<ArticleDTO>?> GetItems([FromQuery] ArticleFilter? args = null)
        {
            try
            {
                var items = await _articlesService.GetListAsync(args);
                if (items == null)
                {
                    return Enumerable.Empty<ArticleDTO>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<ArticleDTO>();
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<ArticleDTO>>> Put(int id, Article item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _articlesService.PostAsync(item);

            if (resp == null)
                return Problem("Error saving article");

            return Ok(resp);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<ArticleDTO>>> Post(Article item)
        {
            var resp = await _articlesService.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }

        
        // DELETE: api/Products/5
        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _articlesService.DeleteAsync(id);

            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error on deleting Article");
            }
            else
                return NoContent();
        }

        

        //// Aggiungi questi metodi al controller Articles esistente

        //[HttpGet("{id}/DomainStates")]
        //public async Task<ActionResult<List<ArticleDomainState>>> GetArticleDomainStates(int id)
        //{
        //    var states = await _context.ArticleDomainStates
        //        .Include(ads => ads.Domain)
        //        .Include(ads => ads.CurrentState)
        //        .Include(ads => ads.LastEvent)
        //        .Where(ads => ads.ArticleId == id)
        //        .ToListAsync();

        //    return states;
        //}

        //[HttpGet("{id}/Events")]
        //public async Task<ActionResult<List<ArticleEvent>>> GetArticleEvents(int id, [FromQuery] int? limit = null)
        //{
        //    var query = _context.ArticleEvents
        //        .Include(e => e.Domain)
        //        .Include(e => e.EventType)
        //        .Include(e => e.FromState)
        //        .Include(e => e.ToState)
        //        .Where(e => e.ArticleId == id)
        //        .OrderByDescending(e => e.OccurredAt);

        //    var events = limit.HasValue
        //        ? await query.Take(limit.Value).ToListAsync()
        //        : await query.ToListAsync();

        //    return events;
        //}

        //[HttpPost("{id}/InitializeDomain/{domainId}")]
        //public async Task<IActionResult> InitializeArticleDomain(int id, int domainId)
        //{
        //    // Verifica che l'articolo esista
        //    var article = await _context.Articles.FindAsync(id);
        //    if (article == null) return NotFound();

        //    // Verifica che il dominio esista
        //    var domain = await _context.ArticleDomains.FindAsync(domainId);
        //    if (domain == null) return NotFound("Dominio non trovato");

        //    // Verifica se già esiste uno stato per questo dominio
        //    var existingState = await _context.ArticleDomainStates
        //        .FirstOrDefaultAsync(ads => ads.ArticleId == id && ads.DomainId == domainId);

        //    if (existingState != null)
        //        return BadRequest("Lo stato per questo dominio è già stato inizializzato");

        //    // Trova lo stato iniziale per questo dominio (quello con SortOrder più basso)
        //    var initialState = await _context.ArticleStates
        //        .Where(s => s.DomainId == domainId && s.IsActive)
        //        .OrderBy(s => s.SortOrder)
        //        .FirstOrDefaultAsync();

        //    if (initialState == null)
        //        return BadRequest("Nessuno stato iniziale trovato per questo dominio");

        //    // Crea il domain state
        //    var domainState = new ArticleDomainState
        //    {
        //        ArticleId = id,
        //        DomainId = domainId,
        //        CurrentStateId = initialState.Id,
        //        UpdatedAt = DateTime.UtcNow
        //    };

        //    _context.ArticleDomainStates.Add(domainState);
        //    await _context.SaveChangesAsync();

        //    return Ok(domainState);
        //}
       


    }
}
