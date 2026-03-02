using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using System.Security.Claims;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Implementazione server del servizio TicketFeedback.
    /// Accede direttamente al database.
    /// </summary>
    public class ArticlesService : IArticlesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        private readonly ILanguagesService _languagesService;
        private readonly ILogEventService _logEventService;
        public ArticlesService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IPermitsService permitsService,
            ILanguagesService languagesService,
            ILogEventService logEventService)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
            _languagesService = languagesService;
            _logEventService = logEventService;
        }

        public async Task<ArticleDTO?> GetItemAsync(int id)
        {
            var item = await _context.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return item.ToDTO() != null ? item.ToDTO() : null;
        }

        public async Task<ArticleDTO?> GetFirstAsync()
        {
            

            var item = await _context.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item.ToDTO() != null ? item.ToDTO() : null;
        }

        public async Task<PagingResponse<ArticleDTO, object>?> GetSummaryAsync(ArticleFilter? args)
        {
            try
            {
                var acceptLanguage = await _languagesService.GetCodeLanguage();
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new();
                }
                int count = items.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }

                var paginationMetadata = new PagingHeaderModel
                {
                    TotalCount = count,
                    PageSize = args != null ? args.PageSize : 0,
                };
                PagingResponse<ArticleDTO, object> resp = new PagingResponse<ArticleDTO, object>()
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesService), nameof(GetSummaryAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<PagingResponse<ArticleDTO>?> GetPagingAsync(ArticleFilter? args = null)
        {
            try
            {
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new();
                }
                int count = items.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }


                var paginationMetadata = new PagingHeaderModel
                {
                    TotalCount = count,
                    PageSize = args != null ? args.PageSize : 0,


                };
                PagingResponse<ArticleDTO> resp = new PagingResponse<ArticleDTO>()
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesService), nameof(GetPagingAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<List<ArticleDTO>?> GetListAsync(ArticleFilter? args = null)
        {
            try
            {
                var acceptLanguage = await _languagesService.GetCodeLanguage();
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new List<ArticleDTO>();
                }

                return await items.Select(item => item.ToDTO()).ToListAsync();
                
                    
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }


        public async Task<APIResponseMessage<ArticleDTO>> PostAsync(Article  item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.Articles.Update(item);
                }
                else
                {
                    _context.Articles.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<ArticleDTO>
                {
                    State = true,
                    Data = item.ToDTO(),
                    Message = "Article saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<ArticleDTO>
                {
                    State = false,
                    Message = "Error saving Article",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Articles.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {

                _context.Articles.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        // Aggiungi questi metodi al controller Articles esistente

        public async Task<ActionResult<List<ArticleDomainState>>> GetArticleDomainStates(int id)
        {
            var states = await _context.ArticleDomainStates
                .Include(ads => ads.Domain)
                .Include(ads => ads.CurrentState)
                .Include(ads => ads.LastEvent)
                .Where(ads => ads.ArticleId == id)
                .ToListAsync();

            return states;
        }

        public async Task<ActionResult<List<ArticleEvent>>> GetArticleEvents(int id, [FromQuery] int? limit = null)
        {
            var query = _context.ArticleEvents
                .Include(e => e.Domain)
                .Include(e => e.EventType)
                .Include(e => e.FromState)
                .Include(e => e.ToState)
                .Where(e => e.ArticleId == id)
                .OrderByDescending(e => e.OccurredAt);

            var events = limit.HasValue
                ? await query.Take(limit.Value).ToListAsync()
                : await query.ToListAsync();

            return events;
        }

        public async Task<ArticleDomainState?> InitializeArticleDomain(int id, int domainId)
        {
            // Verifica che l'articolo esista
            var article = await _context.Articles.FindAsync(id);
            if (article == null) return null;
            // Verifica che il dominio esista
            var domain = await _context.ArticleDomains.FindAsync(domainId);
            if (domain == null) return null;

            // Verifica se già esiste uno stato per questo dominio
            var existingState = await _context.ArticleDomainStates
                .FirstOrDefaultAsync(ads => ads.ArticleId == id && ads.DomainId == domainId);

            if (existingState != null)
                return null;

            // Trova lo stato iniziale per questo dominio (quello con SortOrder più basso)
            var initialState = await _context.ArticleStates
                .Where(s => s.DomainId == domainId && s.IsActive)
                .OrderBy(s => s.SortOrder)
                .FirstOrDefaultAsync();

            if (initialState == null)
                return null;

            // Crea il domain state
            var domainState = new ArticleDomainState
            {
                ArticleId = id,
                DomainId = domainId,
                CurrentStateId = initialState.Id,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ArticleDomainStates.Add(domainState);
            await _context.SaveChangesAsync();

            return domainState;
        }



        private async Task<IQueryable<Article>?> FilterItems(ArticleFilter? args = null)
        {
            try
            {
                var articles = _context.Articles.Include(x => x.Company).Include(x => x.Product).AsQueryable();

                var resp = await _permitsService.GetIdCompanies();

                articles = articles.Where(x => resp.Contains(x.IdCompany ?? 0));


                if (args?.IdCompany != null)
                    articles = articles.Where(x => x.IdCompany == args.IdCompany);

                if (args?.IdProduct != null)
                    articles = articles.Where(x => x.IdProduct == args.IdProduct);

                if (args?.SerialNumber != null && args.SerialNumber.Length > 0)
                    articles = articles.Where(x => x.SerialNumber.Contains(args.SerialNumber));

                if (args?.Filter != null && args.Filter.Trim().Length > 0)
                {
                    articles = articles.Where(args.Filter);
                }

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    articles = articles.OrderBy(args.OrderBy);
                }
                else
                    articles = articles.OrderBy(x => x.Product.Name).ThenBy(x => x.Name);

                return articles;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesService), nameof(FilterItems), EventsTypes.Error, ex);
                return null;
            }
        }
    }
}
