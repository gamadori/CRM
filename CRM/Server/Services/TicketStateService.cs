using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.Resources.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

using System.Linq.Dynamic.Core;

namespace CRM.Server.Services
{
    public class TicketStatesService: ITicketStatesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogEventService _logEventService;

        public TicketStatesService(ApplicationDbContext context, IPermitsService permitsService, IHttpContextAccessor httpContextAccessor, ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _httpContextAccessor = httpContextAccessor; 
            _logEventService = logEventService;
        }

        public async Task<TicketState?> GetItemAsync(int id)
        {
            return await _context.TicketStates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<TicketState?> GetFirstAsync()
        {
            return await _context.TicketStates
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public async Task<PagingResponse<TicketState, object>?> GetSummaryAsync(TicketStateFilter? args)
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
                PagingResponse<TicketState, object> resp = new PagingResponse<TicketState, object>()
                {
                    Items = await items.ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketStatesService), nameof(GetSummaryAsync), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<PagingResponse<TicketState>?> GetPagingAsync(TicketStateFilter? args = null)
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
                PagingResponse<TicketState> resp = new PagingResponse<TicketState>()
                {
                    Items = await items.ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketStatesService), nameof(GetPagingAsync), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<List<TicketState>?> GetListAsync(TicketStateFilter? args = null)
        {
            try
            {
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new List<TicketState>();
                }

                return await items.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketStatesService), nameof(GetListAsync), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        
        public async Task<APIResponseMessage<TicketState>> PostAsync(TicketState item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.TicketStates.Update(item);
                }
                else
                {
                    _context.TicketStates.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<TicketState>
                {
                    State = true,
                    Data = item,
                    Message = "Language saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketStatesService), nameof(PostAsync), LogEvent.EventsTypes.Error, ex);
                return new APIResponseMessage<TicketState>
                {
                    State = false,
                    Message = "Error saving Ticket State",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.TicketStates.FindAsync(id);
            if (item == null)
            {
                return false;
            }
            try
            {
                _context.TicketStates.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketStatesService), nameof(DeleteAsync), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }

        private async Task<IQueryable<TicketState>?> FilterItems(TicketStateFilter? args = null)
        {
            try
            {
                var items = _context.TicketStates.AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderBy(x => x.State).ThenBy(x=>x.Description);


                if (args?.Filter != null && args.Filter.Any())
                {
                    items = items.Where(args.Filter);
                }

                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(FilterItems), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }


        
    }
}
