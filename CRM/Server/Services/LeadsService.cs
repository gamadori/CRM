using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    public class LeadsService : ILeadsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        private readonly IWorkflowAutomationService _workflowAutomationService;

        public LeadsService(
            ApplicationDbContext context,
            IPermitsService permitsService,
            ILogEventService logEventService,
            IWorkflowAutomationService workflowAutomationService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
            _workflowAutomationService = workflowAutomationService;
        }

        public async Task<LeadDTO?> GetItemAsync(int id)
        {
            var item = await BaseQuery().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            var dto = item.ToDTO();
            if (dto != null)
            {
                dto.Permits = ComputePermits(item!.IdUser, await SafeCurrentUserAsync());
            }

            return dto;
        }

        public async Task<PagingResponse<LeadDTO, decimal>?> GetSummaryAsync(LeadFilter? args)
        {
            try
            {
                var q = FilterItems(args);
                var count = await q.CountAsync();
                var total = await q.SumAsync(x => x.EstimatedValue);

                if (args?.Skip != null && args.Top != null)
                {
                    q = q.Skip(args.Skip.Value).Take(args.Top.Value);
                }

                var currentUser = await SafeCurrentUserAsync();
                var items = await q.Select(x => x.ToDTO()).ToListAsync();
                foreach (var item in items.Where(x => x != null))
                {
                    item!.Permits = ComputePermits(item.IdUser, currentUser);
                }

                return new PagingResponse<LeadDTO, decimal>
                {
                    Items = items!,
                    MetaData = new PagingHeaderModel { TotalCount = count, PageSize = args?.PageSize ?? 0 },
                    Total = total
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LeadsService), nameof(GetSummaryAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<List<LeadDTO>?> GetListAsync(LeadFilter? args = null)
        {
            try
            {
                return await FilterItems(args).Select(x => x.ToDTO()).ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LeadsService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<APIResponseMessage<LeadDTO>> PostAsync(Lead item)
        {
            try
            {
                var isNew = item.Id == 0;
                LeadStatus? previousStatus = null;

                if (isNew)
                {
                    item.CreatedAt = item.CreatedAt == default ? DateTime.Now : item.CreatedAt;
                    item.IdUser = string.IsNullOrWhiteSpace(item.IdUser) ? await SafeCurrentUserAsync() : item.IdUser;
                    _context.Leads.Add(item);
                }
                else
                {
                    var existing = await _context.Leads
                        .Include(x => x.ProductInterests)
                        .FirstOrDefaultAsync(x => x.Id == item.Id);
                    if (existing == null)
                    {
                        return Fail("Lead non trovato", System.Net.HttpStatusCode.NotFound);
                    }

                    previousStatus = existing.Status;
                    existing.Name = item.Name;
                    existing.CompanyName = item.CompanyName;
                    existing.JobTitle = item.JobTitle;
                    existing.Email = item.Email;
                    existing.Phone = item.Phone;
                    existing.Source = item.Source;
                    existing.Status = item.Status;
                    existing.Score = item.Score;
                    existing.EstimatedValue = item.EstimatedValue;
                    existing.ExpectedCloseDate = item.ExpectedCloseDate;
                    existing.Note = item.Note;
                    existing.IdCompany = item.IdCompany;
                    existing.IdContact = item.IdContact;
                    existing.IdInitiative = item.IdInitiative;
                    // Solo se ne arriva uno: la modifica di un lead non deve staccare la foto del
                    // biglietto solo perche' la maschera non la rispedisce.
                    existing.IdBusinessCard = item.IdBusinessCard ?? existing.IdBusinessCard;
                    existing.IdUser = string.IsNullOrWhiteSpace(item.IdUser) ? existing.IdUser : item.IdUser;
                    existing.UpdatedAt = DateTime.Now;
                    ReplaceLeadProductInterests(existing, item.ProductInterests);
                    item = existing;
                }

                await SyncCompanyReferenceAsync(item);
                await ResolveProductInterestValuesAsync(item);
                await _context.SaveChangesAsync();

                var loaded = await BaseQuery().FirstAsync(x => x.Id == item.Id);
                if (isNew)
                {
                    await _workflowAutomationService.ExecuteAsync(WorkflowTrigger.LeadCreated, lead: loaded);
                }
                else if (previousStatus != LeadStatus.Qualified && loaded.Status == LeadStatus.Qualified)
                {
                    await _workflowAutomationService.ExecuteAsync(WorkflowTrigger.LeadQualified, lead: loaded);
                }

                return Ok((await GetItemAsync(item.Id))!, "Lead salvato");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LeadsService), nameof(PostAsync), EventsTypes.Error, ex);
                return Fail("Errore nel salvataggio del lead", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<DealDTO>> ConvertAsync(int id, ConvertLeadRequest request)
        {
            try
            {
                var lead = await BaseQuery().FirstOrDefaultAsync(x => x.Id == id);
                if (lead == null)
                {
                    return new APIResponseMessage<DealDTO> { State = false, Message = "Lead non trovato", Code = System.Net.HttpStatusCode.NotFound };
                }

                if (lead.Status == LeadStatus.Converted && lead.ConvertedDealId != null)
                {
                    var existingDeal = await _context.Deals
                        .Include(x => x.Company)
                        .Include(x => x.Contact)
                        .Include(x => x.User)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == lead.ConvertedDealId);

                    return new APIResponseMessage<DealDTO>
                    {
                        State = existingDeal != null,
                        Data = existingDeal.ToDTO(),
                        Message = existingDeal != null ? "Lead gia' convertito" : "Deal convertito non trovato",
                        Code = existingDeal != null ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound
                    };
                }

                await SyncCompanyReferenceAsync(lead);
                await EnsureCompanyAndContactAsync(lead, request);

                var probability = request.Probability ?? Math.Max(lead.Score, 20);
                var deal = new Deal
                {
                    Date = DateTime.Today,
                    Name = string.IsNullOrWhiteSpace(request.DealName) ? lead.Name : request.DealName!,
                    IdCompany = lead.IdCompany,
                    IdContact = lead.IdContact,

                    // L'iniziativa che ha portato il lead segue l'opportunita': e' proprio qui che
                    // l'attribuzione di una fiera si perde se ci si dimentica, e senza di essa il
                    // resoconto mostra i lead raccolti ma non gli affari che ne sono nati.
                    IdInitiative = lead.IdInitiative,
                    Amount = request.Amount ?? lead.EstimatedValue,
                    Target = request.Amount ?? lead.EstimatedValue,
                    Probability = Math.Clamp(probability, 0, 100),
                    ExpectedCloseDate = request.ExpectedCloseDate ?? lead.ExpectedCloseDate,
                    State = DealStates.Open,
                    Phase = DealPhases.InitialContact,
                    Note = lead.Note,
                    IdUser = lead.IdUser ?? await SafeCurrentUserAsync()
                };

                CopyLeadProductInterestsToDeal(lead, deal);
                if (deal.ProductInterests.Count > 0)
                {
                    deal.Amount = deal.ProductInterests.Sum(x => x.LineTotal);
                    deal.Target = deal.Amount;
                }

                _context.Deals.Add(deal);
                lead.Status = LeadStatus.Converted;
                lead.ConvertedAt = DateTime.Now;
                lead.ConvertedDeal = deal;
                lead.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await _workflowAutomationService.ExecuteAsync(WorkflowTrigger.LeadConverted, lead: lead);
                await _workflowAutomationService.ExecuteAsync(WorkflowTrigger.DealCreated, deal: deal);

                var dto = await _context.Deals
                    .Include(x => x.Company)
                    .Include(x => x.Contact)
                    .Include(x => x.ProductInterests)
                        .ThenInclude(x => x.Product)
                    .Include(x => x.User)
                    .AsNoTracking()
                    .Where(x => x.Id == deal.Id)
                    .Select(x => x.ToDTO())
                    .FirstAsync();

                return new APIResponseMessage<DealDTO>
                {
                    State = true,
                    Data = dto,
                    Message = "Lead convertito in opportunita",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LeadsService), nameof(ConvertAsync), EventsTypes.Error, ex);
                return new APIResponseMessage<DealDTO>
                {
                    State = false,
                    Message = "Errore nella conversione del lead",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Leads.FindAsync(id);
            if (item == null)
            {
                return false;
            }

            try
            {
                _context.Leads.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LeadsService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        private async Task EnsureCompanyAndContactAsync(Lead lead, ConvertLeadRequest request)
        {
            if (lead.IdCompany == null && request.CreateCompanyWhenMissing && !string.IsNullOrWhiteSpace(lead.CompanyName))
            {
                var company = new Company
                {
                    RagioneSociale = lead.CompanyName.Trim(),
                    Email = lead.Email,
                    Telefono = lead.Phone,
                    CompanyType = CompanyTypes.Customer,
                    Note = lead.Note ?? string.Empty,
                    Logo = string.Empty,
                    ResellerName = string.Empty
                };

                _context.Companies.Add(company);
                await _context.SaveChangesAsync();
                lead.IdCompany = company.Id;
                lead.Company = company;
            }

            if (lead.IdContact == null && request.CreateContactWhenMissing)
            {
                var (name, surname) = SplitName(lead.Name);
                var contact = new Contact
                {
                    IdCompany = lead.IdCompany,
                    Name = name,
                    Surname = surname,
                    Email = lead.Email ?? string.Empty,
                    Phone = lead.Phone ?? string.Empty,
                    Mobile = string.Empty,
                    Note = lead.Note ?? string.Empty
                };

                _context.Contacts.Add(contact);
                await _context.SaveChangesAsync();
                lead.IdContact = contact.Id;
                lead.Contact = contact;
            }
        }

        private IQueryable<Lead> FilterItems(LeadFilter? args)
        {
            var q = BaseQuery();

            if (!string.IsNullOrWhiteSpace(args?.IdUser))
            {
                q = q.Where(x => x.IdUser == args.IdUser);
            }

            if (args?.Status != null)
            {
                q = q.Where(x => x.Status == args.Status);
            }

            if (args?.Source != null)
            {
                q = q.Where(x => x.Source == args.Source);
            }

            if (args?.IdInitiative != null)
            {
                q = q.Where(x => x.IdInitiative == args.IdInitiative);
            }

            if (args?.DateFrom != null)
            {
                q = q.Where(x => x.CreatedAt >= args.DateFrom);
            }

            if (args?.DateTo != null)
            {
                q = q.Where(x => x.CreatedAt <= args.DateTo);
            }

            if (!string.IsNullOrWhiteSpace(args?.Search))
            {
                var search = args.Search.Trim();
                q = q.Where(x =>
                    x.Name.Contains(search) ||
                    (x.CompanyName != null && x.CompanyName.Contains(search)) ||
                    (x.Email != null && x.Email.Contains(search)) ||
                    (x.Phone != null && x.Phone.Contains(search)) ||
                    (x.Company != null && x.Company.RagioneSociale.Contains(search)));
            }

            return q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Name);
        }

        private IQueryable<Lead> BaseQuery()
            => _context.Leads
                .Include(x => x.Company)
                .Include(x => x.Contact)
                .Include(x => x.ProductInterests)
                    .ThenInclude(x => x.Product)
                .Include(x => x.User)
                .Include(x => x.ConvertedDeal);

        private async Task ResolveProductInterestValuesAsync(Lead lead)
        {
            if (lead.ProductInterests.Count == 0)
            {
                return;
            }

            foreach (var row in lead.ProductInterests.OrderBy(x => x.SortOrder))
            {
                row.Quantity = row.Quantity <= 0 ? 1 : row.Quantity;
                row.DiscountPct = Math.Clamp(row.DiscountPct, 0, 100);

                if (lead.IdCompany != null)
                {
                    var priceListItem = await _context.PriceListItems
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.IdCompany == lead.IdCompany && x.IdProduct == row.IdProduct);

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

            lead.EstimatedValue = lead.ProductInterests.Sum(x => x.LineTotal);
        }

        private static void ReplaceLeadProductInterests(Lead lead, IEnumerable<LeadProductInterest> rows)
        {
            lead.ProductInterests.Clear();
            foreach (var row in rows.Where(x => x.IdProduct > 0).OrderBy(x => x.SortOrder))
            {
                lead.ProductInterests.Add(new LeadProductInterest
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

        private static void CopyLeadProductInterestsToDeal(Lead lead, Deal deal)
        {
            foreach (var row in lead.ProductInterests.Where(x => x.IdProduct > 0).OrderBy(x => x.SortOrder))
            {
                deal.ProductInterests.Add(new DealProductInterest
                {
                    IdProduct = row.IdProduct,
                    Quantity = row.Quantity,
                    UnitPrice = row.UnitPrice,
                    DiscountPct = row.DiscountPct,
                    LineTotal = row.LineTotal,
                    SortOrder = row.SortOrder
                });
            }
        }

        private async Task SyncCompanyReferenceAsync(Lead lead)
        {
            if (lead.IdCompany != null)
            {
                var companyName = await _context.Companies
                    .AsNoTracking()
                    .Where(x => x.Id == lead.IdCompany)
                    .Select(x => x.RagioneSociale)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(companyName))
                {
                    lead.CompanyName = companyName;
                }

                return;
            }

            var idCompany = await ResolveCompanyIdAsync(lead.CompanyName);
            if (idCompany != null)
            {
                lead.IdCompany = idCompany;
            }
        }

        private async Task<int?> ResolveCompanyIdAsync(string? companyName)
        {
            if (string.IsNullOrWhiteSpace(companyName))
            {
                return null;
            }

            var normalizedName = companyName.Trim();
            return await _context.Companies
                .AsNoTracking()
                .Where(x => x.RagioneSociale == normalizedName)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<string?> SafeCurrentUserAsync()
        {
            try { return await _permitsService.IdUser(); }
            catch { return null; }
        }

        private static (string Name, string Surname) SplitName(string fullName)
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0) return ("Lead", "Senza nome");
            if (parts.Length == 1) return (parts[0], "-");

            return (parts[^1], string.Join(" ", parts.Take(parts.Length - 1)));
        }

        private static int ComputePermits(string? ownerId, string? currentUserId)
        {
            var permits = PermitsHelper.SetRead(0);
            permits = PermitsHelper.SetInsert(permits);
            if (string.IsNullOrEmpty(currentUserId) || ownerId == currentUserId || string.IsNullOrEmpty(ownerId))
            {
                permits = PermitsHelper.SetEdit(permits);
                permits = PermitsHelper.SetDelete(permits);
            }

            return permits;
        }

        private static APIResponseMessage<LeadDTO> Fail(string msg, System.Net.HttpStatusCode code)
            => new() { State = false, Message = msg, Code = code };

        private static APIResponseMessage<LeadDTO> Ok(LeadDTO data, string msg)
            => new() { State = true, Data = data, Message = msg, Code = System.Net.HttpStatusCode.OK };
    }
}
