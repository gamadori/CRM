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
    public class ContactsService: IContactsService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;

        public ContactsService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor, IPermitsService permitsService, ILogEventService logEventService)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        public async Task<ContactDTO?> GetItemAsync(int id)
        {
            var item = await _context.Contacts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return item.ToDTO();

        }

        public async Task<ContactDTO?> GetFirstAsync()
        {

            var item = await _context.Contacts
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item.ToDTO();
        }

        public async Task<PagingResponse<ContactDTO, object>?> GetSummaryAsync(ContactFilter? args)
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
                PagingResponse<ContactDTO, object> resp = new PagingResponse<ContactDTO, object>()
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContactsService), nameof(GetSummaryAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<PagingResponse<ContactDTO>?> GetPagingAsync(ContactFilter? args = null)
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
                PagingResponse<ContactDTO> resp = new PagingResponse<ContactDTO>()
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContactsService), nameof(GetPagingAsync), EventsTypes.Error, ex);

                return null;
            }
        }

        public async Task<List<ContactDTO>?> GetListAsync(ContactFilter? args = null)
        {
            try
            {
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new List<ContactDTO>();
                }

                return await items.Select(item => item.ToDTO()).ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContactsService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<APIResponseMessage<ContactDTO>> PostAsync(Contact item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.Contacts.Update(item);
                }
                else
                {
                    _context.Contacts.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<ContactDTO>
                {
                    State = true,
                    Data = item.ToDTO(),

                    Message = "Contact saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContactsService), nameof(PostAsync), EventsTypes.Error, ex);

                return new APIResponseMessage<ContactDTO>
                {
                    State = false,
                    Message = "Error saving Contact",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Contacts.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {
                _context.Contacts.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContactsService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }


        private async Task<IQueryable<Contact>?> FilterItems(ContactFilter? args = null)
        {
            try
            {
                var contacts = _context.Contacts.Include(x => x.Company).Include(x => x.Company).AsQueryable();

                

                var companies = await _permitsService.GetIdCompanies();

                contacts = contacts.Where(x => x.IdCompany != null && companies.Contains(x.IdCompany.Value));

                if (args.Filter != null && args.Filter.Trim().Length > 0)
                {
                    contacts = contacts.Where(args.Filter);
                }

                if (args.IdCompany != null)
                {
                    contacts = contacts.Where(x => x.IdCompany == args.IdCompany);
                }

                if (args.Name != null && args.Name.Trim().Length > 0)
                {
                    string filterName = args.Name.Replace(" ", "");


                    contacts = contacts.Where(x => x.Name.Contains(args.Name) || x.Surname.Contains(args.Name) || (x.Surname + x.Name).Contains(filterName) || (x.Name + x.Surname).Contains(filterName));
                }

                if (args.OrderBy != null && args.OrderBy.Length > 0)
                {
                    contacts = contacts.OrderBy(args.OrderBy);
                }
                else
                    contacts = contacts.OrderBy(x => x.Surname).ThenBy(x => x.Name);

                return contacts;

            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesService), nameof(FilterItems), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }
    }
}
