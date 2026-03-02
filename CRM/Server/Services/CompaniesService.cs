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

        public async Task<APIResponseMessage<CompanyDTO>> PostAsync(Company item)
        {
            try
            {

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
            return company?.Logo;
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

                var comapnyType = await _permitsService.CompanyType();


                switch (comapnyType)
                {
                    case CompanyTypes.Customer:

                        companies = companies.Where(x => x.Id == idCompany);
                        break;

                    case CompanyTypes.Reseller:

                        companies = companies.Where(x => x.Id == idCompany || x.IdReseller == idCompany);
                        break;

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
