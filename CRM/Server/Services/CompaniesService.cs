using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Controllers;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Linq.Dynamic.Core;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    public class CompaniesService: ICompaniesService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;

        public CompaniesService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor, IPermitsService permitsService, ILogEventService logEventService)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        public async Task<CompanyDTO?> GetItemAsync(int id)
        {
            if (!await _permitsService.CanAccessCompany(id))
                return null;

            var item = await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return item.ToDTO();

        }

        public async Task<CompanyDTO?> GetFirstAsync()
        {

            var item = await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item.ToDTO();
        }

        public async Task<PagingResponse<CompanyDTO, object>?> GetSummaryAsync(CompanyFilter? args)
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
                PagingResponse<CompanyDTO, object> resp = new PagingResponse<CompanyDTO, object>()
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesService), nameof(GetSummaryAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<PagingResponse<CompanyDTO>?> GetPagingAsync(CompanyFilter? args = null)
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
                PagingResponse<CompanyDTO> resp = new PagingResponse<CompanyDTO>()
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesService), nameof(GetPagingAsync), EventsTypes.Error, ex);

                return null;
            }
        }

        public async Task<List<CompanyDTO>?> GetListAsync(CompanyFilter? args = null)
        {
            try
            {
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new List<CompanyDTO>();
                }

                return await items.Select(item => item.ToDTO()).ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        /// <summary>L'azienda madre attuale (CompanyType = HeadCompany), o null se non definita.</summary>
        public async Task<CompanyDTO?> GetHeadCompanyAsync()
        {
            var company = await _context.GetHeadCompanyAsync();

            return company?.ToDTO();
        }

        public async Task<APIResponseMessage<CompanyDTO>> PostAsync(Company item)
        {
            try
            {
                if (!await CanSaveCompanyAsync(item))
                {
                    return new APIResponseMessage<CompanyDTO>
                    {
                        State = false,
                        Message = "Not authorized to save Company",
                        Code = System.Net.HttpStatusCode.Forbidden
                    };
                }

                // Invariante: esiste UNA sola azienda madre. Se questa viene salvata come
                // HeadCompany, l'eventuale precedente viene declassata a Cliente. Il tutto in
                // un'unica SaveChanges, quindi atomico: non si può restare con due (o zero) madri.
                if (item.CompanyType == CompanyTypes.HeadCompany)
                {
                    var previousHeads = await _context.Companies
                        .Where(c => c.CompanyType == CompanyTypes.HeadCompany && c.Id != item.Id)
                        .ToListAsync();

                    foreach (var previous in previousHeads)
                        previous.CompanyType = CompanyTypes.Customer;
                }

                if (item.Id > 0)
                {
                    _context.Companies.Update(item);
                }
                else
                {
                    _context.Companies.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<CompanyDTO>
                {
                    State = true,
                    Data = item.ToDTO(),

                    Message = "Company saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesService), nameof(PostAsync), EventsTypes.Error, ex);

                return new APIResponseMessage<CompanyDTO>
                {
                    State = false,
                    Message = "Error saving Company",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }
        public async Task<bool> DeleteAsync(int id)
        {
            if (!await _permitsService.CanWriteCompanyData(id))
                return false;

            var item = await _context.Companies.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {
                _context.Companies.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        public async Task<bool> AddCustomer(CustomerModel item)
        {
            try
            {
                if (!await _permitsService.CanWriteCompanyData(item.IdReseller) ||
                    !await _permitsService.CanWriteCompanyData(item.IdCustomer))
                {
                    return false;
                }

                var customer = await _context.Companies.FindAsync(item.IdCustomer);

                if (customer != null && customer.IdReseller != item.IdReseller)
                {
                    customer.IdReseller = item.IdReseller;
                    await _context.SaveChangesAsync();
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesService), nameof(AddCustomer), EventsTypes.Error, ex);
                return false;
            }
        }
        public async Task<bool> RemoveCustomer(CustomerModel item)
        {
            try
            {
                if (!await _permitsService.CanWriteCompanyData(item.IdReseller) ||
                    !await _permitsService.CanWriteCompanyData(item.IdCustomer))
                {
                    return false;
                }

                var customer = await _context.Companies.FindAsync(item.IdCustomer);

                if (customer != null && customer.IdReseller == item.IdReseller)
                {
                    customer.IdReseller = null;
                    await _context.SaveChangesAsync();
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesService), nameof(RemoveCustomer), EventsTypes.Error, ex);
                return false;
            }
        }

       
        public async Task<IEnumerable<string>> GetEmailAddress(int idCompany)
        {
            List<string> emailAddresses = new List<string>();

            var company = await _context.Companies.Include(x => x.ApplicationUsers).FirstOrDefaultAsync(x => x.Id == idCompany);

            if (company != null)
            {
                if (!string.IsNullOrEmpty(company.Email))
                    emailAddresses.Add(company.Email);

                foreach (var user in company.ApplicationUsers)
                {
                    if (!string.IsNullOrEmpty(user.Email))
                        emailAddresses.Add(user.Email);
                }

            }
            return emailAddresses;
        }

        public async Task<CompanyDTO?> GetUserCompany()
        {
            var user = await _permitsService.GetUser();

            if (user != null)
            {
                var company = await _context.Companies.FindAsync(user.IdCompany);


                return company.ToDTO();
            }
            else
                return null;
        }

        public async Task<string?> GetLogo(int idCompany)
        {
            var company = await _context.Companies.FindAsync(idCompany);
            return company?.Logo ?? "";
        }

        public async Task<List<CompanyTreeNodeDTO>> GetTreeAsync(int? idCompany = null)
        {
            try
            {
                var user = await _permitsService.GetUser();
                if (user == null || user.IdCompany == null)
                    return new List<CompanyTreeNodeDTO>();

                var userCompany = await _context.Companies.FindAsync(user.IdCompany);
                if (userCompany == null)
                    return new List<CompanyTreeNodeDTO>();

                var companyType = await _permitsService.CompanyType();

                // Determina la ditta radice da visualizzare
                int rootCompanyId;

                if (idCompany.HasValue)
                {
                    // Verifica che l'utente possa accedere all'albero della ditta richiesta
                    if (!await CanViewTree(idCompany.Value, user.IdCompany.Value, companyType))
                        return new List<CompanyTreeNodeDTO>();

                    rootCompanyId = idCompany.Value;
                }
                else
                {
                    // Nessun parametro: usa la ditta dell'utente
                    rootCompanyId = user.IdCompany.Value;
                }

                var rootCompany = await _context.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rootCompanyId);
                if (rootCompany == null)
                    return new List<CompanyTreeNodeDTO>();

                // Carica le aziende in base al tipo della ditta radice
                List<Company> allCompanies;

                if (rootCompany.CompanyType == CompanyTypes.HeadCompany)
                {
                    allCompanies = await _context.Companies.AsNoTracking().OrderBy(x => x.RagioneSociale).ToListAsync();
                }
                else if (rootCompany.CompanyType == CompanyTypes.Reseller)
                {
                    var accessibleCompanyIds = await _permitsService.GetIdCompanies(rootCompanyId);
                    allCompanies = await _context.Companies.AsNoTracking()
                        .Where(x => accessibleCompanyIds.Contains(x.Id))
                        .OrderBy(x => x.RagioneSociale).ToListAsync();
                }
                else
                {
                    // Customer: mostra solo se stesso
                    allCompanies = await _context.Companies.AsNoTracking()
                        .Where(x => x.Id == rootCompanyId)
                        .OrderBy(x => x.RagioneSociale).ToListAsync();
                }

                return BuildTree(allCompanies, rootCompany.CompanyType, rootCompanyId);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesService), nameof(GetTreeAsync), EventsTypes.Error, ex);
                return new List<CompanyTreeNodeDTO>();
            }
        }

        /// <summary>
        /// Verifica se l'utente può visualizzare l'albero della ditta richiesta.
        /// - HeadCompany: può vedere qualsiasi albero
        /// - Reseller: può vedere il proprio albero o quello di un suo cliente
        /// - Customer: può vedere solo il proprio albero
        /// </summary>
        private async Task<bool> CanViewTree(int requestedCompanyId, int userCompanyId, CompanyTypes? userCompanyType)
        {
            // Stessa ditta: sempre consentito
            if (requestedCompanyId == userCompanyId)
                return true;

            return await _permitsService.CanAccessCompany(requestedCompanyId);
        }

        private List<CompanyTreeNodeDTO> BuildTree(List<Company> companies, CompanyTypes? viewerType, int? viewerCompanyId)
        {
            var lookup = companies.ToDictionary(c => c.Id);
            var roots = new List<CompanyTreeNodeDTO>();

            if (viewerType == CompanyTypes.HeadCompany)
            {
                // HeadCompany vede tutto: root = HeadCompany, sotto i Reseller, sotto i Customer
                var headCompanies = companies.Where(c => c.CompanyType == CompanyTypes.HeadCompany).ToList();
                var resellers = companies.Where(c => c.CompanyType == CompanyTypes.Reseller).ToList();
                var customers = companies.Where(c => c.CompanyType == CompanyTypes.Customer).ToList();

                foreach (var hc in headCompanies)
                {
                    var node = ToTreeNode(hc);

                    // Aggiungi rivenditori come figli della HeadCompany
                    foreach (var reseller in resellers)
                    {
                        var resellerNode = ToTreeNode(reseller);

                        // Aggiungi clienti del rivenditore
                        foreach (var customer in customers.Where(c => c.IdReseller == reseller.Id))
                        {
                            resellerNode.Children.Add(ToTreeNode(customer));
                        }

                        node.Children.Add(resellerNode);
                    }

                    // Clienti senza rivenditore (assegnati direttamente alla HeadCompany o senza reseller)
                    foreach (var customer in customers.Where(c => c.IdReseller == null || (!resellers.Any(r => r.Id == c.IdReseller))))
                    {
                        node.Children.Add(ToTreeNode(customer));
                    }

                    roots.Add(node);
                }

                // Se non ci sono HeadCompany, mostra tutto flat
                if (!headCompanies.Any())
                {
                    foreach (var c in companies)
                        roots.Add(ToTreeNode(c));
                }
            }
            else if (viewerType == CompanyTypes.Reseller)
            {
                // Reseller vede se stesso come root con i suoi clienti
                var reseller = companies.FirstOrDefault(c => c.Id == viewerCompanyId);
                if (reseller != null)
                {
                    var node = ToTreeNode(reseller);
                    AddCompanyChildren(node, companies, reseller.Id);
                    roots.Add(node);
                }
            }
            else
            {
                // Customer vede solo se stesso
                foreach (var c in companies)
                    roots.Add(ToTreeNode(c));
            }

            return roots;
        }

        private static CompanyTreeNodeDTO ToTreeNode(Company c)
        {
            return new CompanyTreeNodeDTO
            {
                Id = c.Id,
                RagioneSociale = c.RagioneSociale,
                CompanyType = c.CompanyType,
                Citta = c.Citta,
                Email = c.Email
            };
        }

        private static void AddCompanyChildren(CompanyTreeNodeDTO parentNode, List<Company> companies, int parentCompanyId)
        {
            foreach (var child in companies.Where(c => c.IdReseller == parentCompanyId).OrderBy(c => c.RagioneSociale))
            {
                var childNode = ToTreeNode(child);
                AddCompanyChildren(childNode, companies, child.Id);
                parentNode.Children.Add(childNode);
            }
        }

        private async Task<bool> CanSaveCompanyAsync(Company item)
        {
            if (item.CompanyType == CompanyTypes.HeadCompany && !await _permitsService.CanManageSettings())
                return false;

            if (item.Id > 0)
                return await _permitsService.CanWriteCompanyData(item.Id);

            if (await _permitsService.BelongsToHeadCompany())
                return await _permitsService.IsStandardUser();

            if (item.IdReseller == null)
                return false;

            return await _permitsService.CanWriteCompanyData(item.IdReseller);
        }

        private async Task<IQueryable<Company>?> FilterItems(CompanyFilter? args = null)
        {
            try
            {
                int? idCompany;

                idCompany = await _permitsService.GetIdCompany();

                var companies = _context.Companies.AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    companies = companies.OrderBy(args.OrderBy);
                }
                else
                    companies = companies.OrderBy(x => x.RagioneSociale);


                if (args?.Filter != null && args.Filter.Any())
                {
                    companies = companies.Where(args.Filter);
                }

                var companyType = await _permitsService.CompanyType();
                if (companyType != CompanyTypes.HeadCompany)
                {
                    var accessibleCompanyIds = await _permitsService.GetIdCompanies();
                    companies = companies.Where(x => accessibleCompanyIds.Contains(x.Id));
                }

                if (args?.RagioneSociale != null && args.RagioneSociale.Length > 0)
                {
                    companies = companies.Where(x => x.RagioneSociale.Contains(args.RagioneSociale));
                }

                if (args?.Stato != null && args.Stato.Length > 0)
                {
                    companies = companies.Where(x => x.Stato.Contains(args.Stato));
                }

                if (args?.IdReseller != null && args.IdReseller > 0)
                {
                    companies = companies.Where(x => x.IdReseller == args.IdReseller);
                }

                if (args?.CompanyType != null)
                {
                    companies = companies.Where(x => x.CompanyType == args.CompanyType);
                }

                if (args?.IdCompanyParent != null)
                {
                    companies = companies.Where(x => x.IdReseller != args.IdCompanyParent && x.CompanyType != CompanyTypes.HeadCompany && x.Id != args.IdCompanyParent);
                }


                if (args != null && args.Reseller)
                {
                    companies = companies.Where(x => x.CompanyType == CompanyTypes.Reseller || x.CompanyType == CompanyTypes.HeadCompany);
                }
                return companies;


            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesService), nameof(FilterItems), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }
    }
}
