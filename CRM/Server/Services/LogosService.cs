
using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;



namespace CRM.Server.Services
{
    public class LogosService: ILogosService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogEventService _logEventService;

        public LogosService(ApplicationDbContext context, IPermitsService permitsService, IHttpContextAccessor httpContextAccessor, ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _httpContextAccessor = httpContextAccessor; 
            _logEventService = logEventService;
        }

        public async Task<Logo?> GetItemAsync(int id)
        {
            return await _context.Logos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<Logo?> GetFirstAsync()
        {
            return await _context.Logos
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public async Task<PagingResponse<Logo, object>?> GetSummaryAsync(LogosFilterModel? args)
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
                PagingResponse<Logo, object> resp = new PagingResponse<Logo, object>()
                {
                    Items = await items.ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LogosService), nameof(GetSummaryAsync), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<PagingResponse<Logo>?> GetPagingAsync(LogosFilterModel? args = null)
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
                PagingResponse<Logo> resp = new PagingResponse<Logo>()
                {
                    Items = await items.ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LogosService), nameof(GetPagingAsync), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<List<Logo>?> GetListAsync(LogosFilterModel? args = null)
        {
            try
            {
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new List<Logo>();
                }

                return await items.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LogosService), nameof(GetPagingAsync), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        
        public async Task<APIResponseMessage<Logo>> PostAsync(Logo item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.Logos.Update(item);
                }
                else
                {
                    _context.Logos.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<Logo>
                {
                    State = true,
                    Data = item,
                    Message = "Logo saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LogosService), nameof(PostAsync), LogEvent.EventsTypes.Error, ex);
                return new APIResponseMessage<Logo>
                {
                    State = false,
                    Message = "Error saving Logo",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Logos.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {
                _context.EmailTemplates.Where(x => x.IdLogo == id).ToList().ForEach(x => x.IdLogo = null);  
                _context.Logos.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LogosService), nameof(DeleteAsync), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }


        
        
     
        private async Task<IQueryable<Logo>?> FilterItems(LogosFilterModel? args = null)
        {
            try
            {
                var items = _context.Logos.AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderBy(x => x.Codice).ThenBy(x=>x.Descrizione);


                if (args?.Filter != null && args.Filter.Any())
                {
                    items = items.Where(args.Filter);
                }

                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LogosService), nameof(FilterItems), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }


        
    }
}
