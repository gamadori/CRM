using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

using System.Linq.Dynamic.Core;

namespace CRM.Server.Services
{
    public class LanguagesService: ILanguagesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogEventService _logEventService;

        public LanguagesService(ApplicationDbContext context, IPermitsService permitsService, IHttpContextAccessor httpContextAccessor, ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _httpContextAccessor = httpContextAccessor; 
            _logEventService = logEventService;
        }

        public async Task<Language?> GetItemAsync(int id)
        {
            return await _context.Languages
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<Language?> GetFirstAsync()
        {
            return await _context.Languages
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public async Task<PagingResponse<Language, object>?> GetSummaryAsync(LanguageFilter? args)
        {
            try
            {
                var items = await FilterLanguages(args);

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
                PagingResponse<Language, object> resp = new PagingResponse<Language, object>()
                {
                    Items = await items.ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesService), nameof(GetSummaryAsync), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<PagingResponse<Language>?> GetPagingAsync(LanguageFilter? args = null)
        {
            try
            {

                var items = await FilterLanguages(args);

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
                PagingResponse<Language> resp = new PagingResponse<Language>()
                {
                    Items = await items.ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesService), nameof(GetPagingAsync), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<List<Language>?> GetListAsync(LanguageFilter? args = null)
        {
            try
            {
                var items = await FilterLanguages(args);

                if (items == null)
                {
                    return new List<Language>();
                }

                return await items.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesService), nameof(GetPagingAsync), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        
        public async Task<APIResponseMessage<Language>> PostAsync(Language item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.Languages.Update(item);
                }
                else
                {
                    _context.Languages.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<Language>
                {
                    State = true,
                    Data = item,
                    Message = "Language saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesService), nameof(PostAsync), LogEvent.EventsTypes.Error, ex);
                return new APIResponseMessage<Language>
                {
                    State = false,
                    Message = "Error saving language",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var language = await _context.Languages.FindAsync(id);
            if (language == null)
            {
                return false;
            }
            try
            {
                _context.Languages.Remove(language);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesService), nameof(DeleteAsync), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }


        public async Task<int?> GetIdLanguage()
        {
            var user = await _permitsService.GetUser();
            if (user != null)
            {
                var language = _context.Languages.FirstOrDefault(x => x.LanguageCode == user.LanguageCode);

                return language?.Id;
            }
            else
                return null;
        }

        public async Task<string?> GetCodeLanguage()
        {
            var user = await _permitsService.GetUser();
            if (user != null)
            {


                return user.LanguageCode; 
            }
            else
                return null;
        }

        public async Task<bool> SetIdLanguage(int id)
        {
            try
            {
                var user = await _permitsService.GetUser();
                if (user != null)
                {
                    var language = await _context.Languages.FindAsync(id);
                    if (language != null)
                    {
                        user.LanguageCode = language.LanguageCode;
                        _context.Users.Update(user);
                        await _context.SaveChangesAsync();
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesService), nameof(SetIdLanguage), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }
        public async Task<bool> SetCodeLanguage(string code)
        {
            try
            {
                var user = await _permitsService.GetUser();
                if (user != null)
                {
                    var language = await _context.Languages.Where(x=>x.LanguageCode == code).FirstOrDefaultAsync();
                    if (language != null)
                    {
                        user.LanguageCode = language.LanguageCode;
                        _context.Users.Update(user);
                        await _context.SaveChangesAsync();
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesService), nameof(SetIdLanguage), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }
        private async Task<IQueryable<Language>?> FilterLanguages(LanguageFilter? args = null)
        {
            try
            {
                var items = _context.Languages.AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderBy(x => x.Index).ThenBy(x=>x.Name);


                if (args?.Filter != null && args.Filter.Any())
                {
                    items = items.Where(args.Filter);
                }

                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesService), nameof(FilterLanguages), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }


        
    }
}
