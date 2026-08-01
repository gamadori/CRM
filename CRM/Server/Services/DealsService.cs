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
        private readonly IWorkflowAutomationService _workflowAutomationService;
        
        public DealsService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor,
            IPermitsService permitsService,
            ILogEventService logEventService,
            IWorkflowAutomationService workflowAutomationService)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
            _logEventService = logEventService;
            _workflowAutomationService = workflowAutomationService;
        }

        public async Task<DealDTO?> GetItemAsync(int id)
        {
            var item = await _context.Deals
                .Include(x => x.Company)
                .Include(x => x.Contact)
                .Include(x => x.ProductInterests)
                    .ThenInclude(x => x.Product)
                .Include(x => x.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            // Perimetro aziende: una trattativa di un'altra azienda non esiste, per questo utente.
            if (item != null && !await CanAccessAsync(item.IdCompany))
                return null;

            var dto = item.ToDTO();
            if (dto != null)
            {
                dto.Permits = await _permitsService.DealPermits(dto.Id);
            }

            return dto;
            
        }

        public async Task<DealDTO?> GetFirstAsync()
        {

            var q = _context.Deals
                .Include(x => x.Company)
                .Include(x => x.Contact)
                .Include(x => x.ProductInterests)
                    .ThenInclude(x => x.Product)
                .AsNoTracking()
                .AsQueryable();

            // Perimetro aziende: la "prima trattativa" dev'essere la prima fra quelle visibili.
            var allowed = await _permitsService.GetVisibleCompanyIds();
            if (allowed != null)
                q = q.Where(x => x.IdCompany != null && allowed.Contains(x.IdCompany.Value));

            var item = await q.FirstOrDefaultAsync();
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

        public async Task<CommercialForecastDTO?> GetForecastAsync(DealForecastFilter? args)
        {
            try
            {
                var dateFrom = args?.DateFrom?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var dateTo = args?.DateTo?.Date ?? dateFrom.AddMonths(6).AddDays(-1);

                var q = _context.Deals
                    .Include(x => x.Company)
                    .Include(x => x.User)
                    .AsNoTracking()
                    .Where(x => x.State != DealStates.Missing);

                // Perimetro aziende: il forecast interroga direttamente il DbSet, quindi il
                // filtro va applicato anche qui e non solo in FilterItems.
                var allowedForecast = await _permitsService.GetVisibleCompanyIds();
                if (allowedForecast != null)
                    q = q.Where(x => x.IdCompany != null && allowedForecast.Contains(x.IdCompany.Value));

                q = q.Where(x =>
                    (x.ExpectedCloseDate != null && x.ExpectedCloseDate.Value.Date >= dateFrom && x.ExpectedCloseDate.Value.Date <= dateTo) ||
                    (x.ExpectedCloseDate == null && x.Date.Date >= dateFrom && x.Date.Date <= dateTo));

                if (!string.IsNullOrWhiteSpace(args?.IdUser))
                {
                    q = q.Where(x => x.IdUser == args.IdUser);
                }

                var deals = await q.ToListAsync();

                var forecast = new CommercialForecastDTO
                {
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    DealCount = deals.Count,
                    OpenPipeline = deals.Where(IsOpenPipeline).Sum(x => x.Amount),
                    WeightedPipeline = deals.Where(IsOpenPipeline).Sum(WeightedAmount),
                    WonAmount = deals.Where(x => x.State == DealStates.CloseWon).Sum(x => x.Amount),
                    LostAmount = deals.Where(x => x.State == DealStates.CloseLost).Sum(x => x.Amount),
                    TargetAmount = deals.Sum(x => x.Target)
                };

                forecast.ByMonth = deals
                    .GroupBy(x => (x.ExpectedCloseDate ?? x.Date).ToString("yyyy-MM"))
                    .OrderBy(x => x.Key)
                    .Select(x => new CommercialForecastBucketDTO
                    {
                        Key = x.Key,
                        Label = x.Key,
                        DealCount = x.Count(),
                        Amount = x.Where(IsOpenPipeline).Sum(d => d.Amount),
                        WeightedAmount = x.Where(IsOpenPipeline).Sum(WeightedAmount),
                        WonAmount = x.Where(d => d.State == DealStates.CloseWon).Sum(d => d.Amount),
                        TargetAmount = x.Sum(d => d.Target)
                    })
                    .ToList();

                forecast.ByOwner = deals
                    .GroupBy(x => x.User?.NameComplete ?? "Senza owner")
                    .OrderByDescending(x => x.Sum(WeightedAmount))
                    .Select(x => new CommercialForecastBucketDTO
                    {
                        Key = x.Key,
                        Label = x.Key,
                        DealCount = x.Count(),
                        Amount = x.Where(IsOpenPipeline).Sum(d => d.Amount),
                        WeightedAmount = x.Where(IsOpenPipeline).Sum(WeightedAmount),
                        WonAmount = x.Where(d => d.State == DealStates.CloseWon).Sum(d => d.Amount),
                        TargetAmount = x.Sum(d => d.Target)
                    })
                    .ToList();

                forecast.ByPhase = deals
                    .GroupBy(x => x.Phase)
                    .OrderBy(x => x.Key)
                    .Select(x => new CommercialForecastBucketDTO
                    {
                        Key = x.Key.ToString(),
                        Label = x.Key.ToString(),
                        DealCount = x.Count(),
                        Amount = x.Where(IsOpenPipeline).Sum(d => d.Amount),
                        WeightedAmount = x.Where(IsOpenPipeline).Sum(WeightedAmount),
                        WonAmount = x.Where(d => d.State == DealStates.CloseWon).Sum(d => d.Amount),
                        TargetAmount = x.Sum(d => d.Target)
                    })
                    .ToList();

                return forecast;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(DealsService), nameof(GetForecastAsync), EventsTypes.Error, ex);
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
                // Perimetro aziende: non si scrive per un'azienda che non si può vedere.
                if (!await CanAccessAsync(item.IdCompany))
                    return new APIResponseMessage<DealDTO>
                    {
                        State = false,
                        Message = "Azienda non accessibile per questo utente",
                        Code = System.Net.HttpStatusCode.Forbidden
                    };

                var isNew = item.Id == 0;
                DealStates? previousState = null;

                if (item.Id > 0)
                {
                    var existing = await _context.Deals
                        .Include(x => x.ProductInterests)
                        .FirstOrDefaultAsync(x => x.Id == item.Id);

                    // Non si modifica una trattativa altrui spacciandola per propria.
                    if (existing != null && !await CanAccessAsync(existing.IdCompany))
                        existing = null;

                    if (existing == null)
                    {
                        return new APIResponseMessage<DealDTO>
                        {
                            State = false,
                            Message = "Deal non trovato",
                            Code = System.Net.HttpStatusCode.NotFound
                        };
                    }

                    if (string.IsNullOrWhiteSpace(item.IdUser))
                    {
                        item.IdUser = existing.IdUser;
                    }

                    previousState = existing.State;

                    if (item.Probability == 0)
                    {
                        item.Probability = DefaultProbability(item.Phase, item.State);
                    }

                    existing.Date = item.Date;
                    existing.Name = item.Name;
                    existing.IdCompany = item.IdCompany;
                    existing.IdContact = item.IdContact;
                    existing.Amount = item.Amount;
                    existing.Target = item.Target;
                    existing.Probability = item.Probability;
                    existing.ExpectedCloseDate = item.ExpectedCloseDate;
                    existing.Note = item.Note;
                    existing.State = item.State;
                    existing.Phase = item.Phase;
                    existing.DateClosed = item.DateClosed;
                    existing.IdUser = item.IdUser;
                    // IdActivityOrigin non si aggiorna: da quale visita e' nata l'opportunita' e'
                    // un fatto avvenuto, non una proprieta' che si cambia in modifica.
                    ReplaceDealProductInterests(existing, item.ProductInterests);
                    await ResolveProductInterestValuesAsync(existing);
                    item = existing;
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

                    if (item.Probability == 0)
                    {
                        item.Probability = DefaultProbability(item.Phase, item.State);
                    }

                    _context.Deals.Add(item);
                    await ResolveProductInterestValuesAsync(item);
                }
                await _context.SaveChangesAsync();

                var saved = await _context.Deals
                    .Include(x => x.Company)
                    .Include(x => x.Contact)
                    .Include(x => x.ProductInterests)
                        .ThenInclude(x => x.Product)
                    .Include(x => x.User)
                    .FirstOrDefaultAsync(x => x.Id == item.Id);

                if (saved != null)
                {
                    if (isNew)
                    {
                        await _workflowAutomationService.ExecuteAsync(WorkflowTrigger.DealCreated, deal: saved);
                    }
                    else if (previousState != DealStates.CloseWon && saved.State == DealStates.CloseWon)
                    {
                        await _workflowAutomationService.ExecuteAsync(WorkflowTrigger.DealWon, deal: saved);
                    }
                    else if (previousState != DealStates.CloseLost && saved.State == DealStates.CloseLost)
                    {
                        await _workflowAutomationService.ExecuteAsync(WorkflowTrigger.DealLost, deal: saved);
                    }
                }

                return new APIResponseMessage<DealDTO>
                {
                    State = true,
                    Data = saved.ToDTO(),
                    
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

        [AuthorizeRole(ePolicy.StandardRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Deals.FindAsync(id);

            if (item == null || !await CanAccessAsync(item.IdCompany))
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

       
       
        /// <summary>
        /// True se l'utente corrente può vedere i dati dell'azienda indicata. Una trattativa senza
        /// azienda è visibile solo a chi vede tutto (azienda madre): fail-closed.
        /// </summary>
        private async Task<bool> CanAccessAsync(int? idCompany)
        {
            var allowed = await _permitsService.GetVisibleCompanyIds();
            if (allowed == null)
                return true;

            return idCompany != null && allowed.Contains(idCompany.Value);
        }

        private async Task<IQueryable<Deal>?> FilterItems(DealFilter? args = null)
        {
            try
            {
                var items = _context.Deals
                    .Include(x => x.Contact)
                    .Include(x => x.Company)
                    .Include(x => x.ProductInterests)
                        .ThenInclude(x => x.Product)
                    .Include(x => x.User)
                    .AsQueryable();

                // Perimetro aziende dell'utente: senza questo filtro un utente cliente o
                // rivenditore vedrebbe le trattative di tutte le aziende dell'installazione.
                var allowed = await _permitsService.GetVisibleCompanyIds();
                if (allowed != null)
                    items = items.Where(x => x.IdCompany != null && allowed.Contains(x.IdCompany.Value));

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

                if (args?.ExpectedCloseFrom != null)
                {
                    items = items.Where(x => x.ExpectedCloseDate >= args.ExpectedCloseFrom);
                }

                if (args?.ExpectedCloseTo != null)
                {
                    items = items.Where(x => x.ExpectedCloseDate <= args.ExpectedCloseTo);
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

        private static bool IsOpenPipeline(Deal deal)
            => deal.State == DealStates.Open || deal.State == DealStates.Suspended;

        private static decimal WeightedAmount(Deal deal)
            => deal.State switch
            {
                DealStates.CloseWon => deal.Amount,
                DealStates.CloseLost => 0,
                _ => Math.Round(deal.Amount * deal.Probability / 100m, 2)
            };

        private static int DefaultProbability(DealPhases phase, DealStates state)
        {
            if (state == DealStates.CloseWon) return 100;
            if (state == DealStates.CloseLost || phase == DealPhases.Lost) return 0;

            return phase switch
            {
                DealPhases.InitialContact => 15,
                DealPhases.NeedsChecked => 30,
                DealPhases.DecisionMakingPhase => 50,
                DealPhases.OfferSubmitted => 70,
                DealPhases.Obtained => 90,
                _ => 10
            };
        }

        private async Task ResolveProductInterestValuesAsync(Deal deal)
        {
            if (deal.ProductInterests.Count == 0)
            {
                return;
            }

            foreach (var row in deal.ProductInterests.OrderBy(x => x.SortOrder))
            {
                row.Quantity = row.Quantity <= 0 ? 1 : row.Quantity;
                row.DiscountPct = Math.Clamp(row.DiscountPct, 0, 100);

                if (deal.IdCompany != null)
                {
                    var priceListItem = await _context.PriceListItems
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.IdCompany == deal.IdCompany && x.IdProduct == row.IdProduct);

                    if (priceListItem != null)
                    {
                        row.UnitPrice = priceListItem.UnitPrice;
                        row.DiscountPct = priceListItem.DiscountPct;
                        row.LineTotal = ProductInterestHelper.CalculateTotal(row.Quantity, row.UnitPrice, row.DiscountPct);
                        continue;
                    }
                }

                if (row.UnitPrice <= 0)
                {
                    row.UnitPrice = await _context.Products
                        .AsNoTracking()
                        .Where(x => x.Id == row.IdProduct)
                        .Select(x => x.Price)
                        .FirstOrDefaultAsync();
                }

                row.LineTotal = ProductInterestHelper.CalculateTotal(row.Quantity, row.UnitPrice, row.DiscountPct);
            }

            deal.Amount = deal.ProductInterests.Sum(x => x.LineTotal);
            deal.Target = deal.Amount;
        }

        private static void ReplaceDealProductInterests(Deal deal, IEnumerable<DealProductInterest> rows)
        {
            deal.ProductInterests.Clear();
            foreach (var row in rows.Where(x => x.IdProduct > 0).OrderBy(x => x.SortOrder))
            {
                deal.ProductInterests.Add(new DealProductInterest
                {
                    IdProduct = row.IdProduct,
                    Quantity = row.Quantity <= 0 ? 1 : row.Quantity,
                    UnitPrice = row.UnitPrice,
                    DiscountPct = Math.Clamp(row.DiscountPct, 0, 100),
                    LineTotal = row.LineTotal,
                    SortOrder = row.SortOrder
                });
            }
        }
    }
}
