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
    /// Implementazione server del servizio Products.
    /// Accede direttamente al database.
    /// </summary>
    public class DealsService : IDealsService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        
        public DealsService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor,
            IPermitsService permitsService,
            ILogEventService logEventService)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        public async Task<DealDTO?> GetItemAsync(int id)
        {
            var item = await _context.Deals
                .Include(x => x.Company)
                .Include(x => x.Contact)
                .Include(x => x.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            var dto = item.ToDTO();
            if (dto != null)
            {
                dto.Permits = await _permitsService.DealPermits(dto.Id);
            }

            return dto;
            
        }

        public async Task<DealDTO?> GetFirstAsync()
        {

            var item = await _context.Deals
                .Include(x => x.Company)
                .Include(x => x.Contact)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item.ToDTO();
        }

       

        public async Task<PagingResponse<DealDTO, decimal>?> GetSummaryAsync(DealFilter? args)
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
                PagingResponse<DealDTO, decimal> resp = new PagingResponse<DealDTO, decimal>()
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = await (await FilterItems(args))!.SumAsync(x => x.Amount),
                };

                foreach (var deal in resp.Items)
                {
                    deal.Permits = await _permitsService.DealPermits(deal.Id);
                }

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(DealsService), nameof(GetSummaryAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<PagingResponse<DealDTO>?> GetPagingAsync(DealFilter? args = null)
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
                PagingResponse<DealDTO> resp = new PagingResponse<DealDTO>()
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                foreach (var deal in resp.Items)
                {
                    deal.Permits = await _permitsService.DealPermits(deal.Id);
                }

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsService), nameof(GetPagingAsync), EventsTypes.Error, ex);

                return null;
            }
        }

        public async Task<List<DealDTO>?> GetListAsync(DealFilter? args = null)
        {
            try
            {
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new List<DealDTO>();
                }

                return await items.Select(item => item.ToDTO()).ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(DealsService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }


        public async Task<APIResponseMessage<DealDTO>> PostAsync(Deal  item)
        {
            try
            {

                if (item.Id > 0)
                {
                    var existing = await _context.Deals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.Id);
                    if (existing != null && string.IsNullOrWhiteSpace(item.IdUser))
                    {
                        item.IdUser = existing.IdUser;
                    }

                    _context.Deals.Update(item);
                }
                else
                {
                    if (item.Date == default)
                    {
                        item.Date = DateTime.Now;
                    }

                    if (string.IsNullOrWhiteSpace(item.IdUser))
                    {
                        item.IdUser = await _permitsService.IdUser();
                    }

                    _context.Deals.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<DealDTO>
                {
                    State = true,
                    Data = item.ToDTO(),
                    
                    Message = "Deal saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(DealsService), nameof(PostAsync), EventsTypes.Error, ex);

                return new APIResponseMessage<DealDTO>
                {
                    State = false,
                    Message = "Error saving Deal",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Deals.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {
                _context.Deals.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(DealsService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

       
       
        private async Task<IQueryable<Deal>?> FilterItems(DealFilter? args = null)
        {
            try
            {
                var items = _context.Deals
                    .Include(x => x.Contact)
                    .Include(x => x.Company)
                    .Include(x => x.User)
                    .AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderByDescending(x => x.Date).ThenBy(x => x.Name);

                if (args?.IdUser != null)
                {
                    items = items.Where(x => x.IdUser == args.IdUser);
                }

                if (args?.State != null)
                {
                    items = items.Where(x => x.State == args.State);
                }

                if (args?.Phase != null)
                {
                    items = items.Where(x => x.Phase == args.Phase);
                }

                if (!string.IsNullOrWhiteSpace(args?.Search))
                {
                    var search = args.Search.Trim();
                    items = items.Where(x =>
                        x.Name.Contains(search) ||
                        (x.Note != null && x.Note.Contains(search)) ||
                        (x.Company != null && x.Company.RagioneSociale.Contains(search)) ||
                        (x.Contact != null && x.Contact.Name.Contains(search)) ||
                        (x.Contact != null && x.Contact.Surname.Contains(search)));
                }

                if (!string.IsNullOrWhiteSpace(args?.Filter))
                {
                    items = items.Where(args.Filter);
                }

                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsService), nameof(FilterItems), EventsTypes.Error, ex);

                return null;
            }
        }
    }
}
