
using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using static CRM.Shared.LogEvent;
using System.Linq.Dynamic.Core;



namespace CRM.Server.Services
{
    public class LogEventService: ILogEventService
    {       
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;


        public LogEventService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<LogEvent?> GetItemAsync(int id)
        {
            return await _context.LogEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<LogEvent?> GetFirstAsync()
        {
            return await _context.LogEvents
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public async Task<PagingResponse<LogEvent, object>?> GetSummaryAsync(LogEventFilterModel? args)
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
                PagingResponse<LogEvent, object> resp = new PagingResponse<LogEvent, object>()
                {
                    Items = await items.ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
               
                return null;
            }
        }

        public async Task<PagingResponse<LogEvent>?> GetPagingAsync(LogEventFilterModel? args = null)
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
                PagingResponse<LogEvent> resp = new PagingResponse<LogEvent>()
                {
                    Items = await items.ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<List<LogEvent>?> GetListAsync(LogEventFilterModel? args = null)
        {
            try
            {
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new List<LogEvent>();
                }

                return await items.ToListAsync();
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public async Task<APIResponseMessage<LogEvent>> PostAsync(LogEvent item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.LogEvents.Update(item);
                }
                else
                {
                    _context.LogEvents.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<LogEvent>
                {
                    State = true,
                    Data = item,
                    Message = "Logo Event saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<LogEvent>
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
            var item = await _context.LogEvents.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {
                
                _context.LogEvents.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }



        public void Register(string module, string subroutine, EventsTypes type, string message)
        {
            LogEvent logEvent = CreateLogEvent(module, subroutine, type, message);

            _context.LogEvents.Add(logEvent);

            _context.SaveChanges();
        }

        public async Task RegisterAsync(string module, string subroutine, EventsTypes type, string message)
        {
            LogEvent logEvent = CreateLogEvent(module, subroutine, type, message);
            
            _context.LogEvents.Add(logEvent);

            await _context.SaveChangesAsync();
        }
        public async Task RegisterAsync(string module, string subroutine, EventsTypes type, Exception ex)
        {
            string msg = $"{ex.Message}\n\r{ex.StackTrace}";
            LogEvent logEvent = CreateLogEvent(module, subroutine, type, msg);

            _context.LogEvents.Add(logEvent);

            await _context.SaveChangesAsync();
        }
        

        private LogEvent CreateLogEvent(string module, string subroutine, EventsTypes type, string massage)
        {
            LogEvent logEvent = new LogEvent();

            logEvent.Module = module;
            logEvent.Subroutine = subroutine;
            logEvent.EventType = type;
            logEvent.Message = massage;
            logEvent.DateEvent = DateTime.Now;
            logEvent.User = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "not loged";
            return logEvent;
        }

        private async Task<IQueryable<LogEvent>?> FilterItems(LogEventFilterModel? args = null)
        {
            try
            {
                var items = _context.LogEvents.AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderByDescending(x => x.DateEvent);

                if (args?.EventType != null && args?.EventType != EventsTypes.NullReference)
                {
                    items = items.Where(x => x.EventType == args.EventType);
                }

                if (args?.Filter != null && args.Filter.Any())
                {
                    items = items.Where(args.Filter);
                }

                return items;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
