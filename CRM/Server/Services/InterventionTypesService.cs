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
    /// Implementazione server del servizio TicketFeedback.
    /// Accede direttamente al database.
    /// </summary>
    public class InterventionTypesService : IInterventionTypesService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        public InterventionTypesService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor,
            IPermitsService permitsService)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
        }

        public async Task<InterventionTypeDTO?> GetItemAsync(int id)
        {
            var acceptLanguage = GetLanguage();
            var item = await _context.InterventionTypes
                .Include(x => x.InterventionTypeLanguages)
                    .ThenInclude(x => x.Language)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return item != null ? new InterventionTypeDTO()
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Translate = item.InterventionTypeLanguages?.FirstOrDefault(x => x.Language?.LanguageCode == acceptLanguage)?.Name ?? item.Name
            } : null;
        }

        public async Task<InterventionTypeDTO?> GetFirstAsync()
        {
            var acceptLanguage = GetLanguage();
            var item = await _context.InterventionTypes
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item != null ? new InterventionTypeDTO()
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Translate = item.InterventionTypeLanguages.Where(x => x.Language.LanguageCode == acceptLanguage).Select(x => x.Name).FirstOrDefault() ?? item.Name
            } : null;
        }

        public async Task<PagingResponse<InterventionTypeDTO, object>?> GetSummaryAsync(InterventionTypeFilter? args)
        {
            try
            {
                var items = FilterItems(args);

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
                PagingResponse<InterventionTypeDTO, object> resp = new PagingResponse<InterventionTypeDTO, object>()
                {
                    Items = await items.Select(item => new InterventionTypeDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        Translate = item.InterventionTypeLanguages.Where(x => x.Language.LanguageCode == GetLanguage()).Select(x => x.Name).FirstOrDefault() ?? item.Name

                    }).ToListAsync(),
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

        public async Task<PagingResponse<InterventionTypeDTO>?> GetPagingAsync(InterventionTypeFilter? args = null)
        {
            try
            {

                var items = FilterItems(args);

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
                PagingResponse<InterventionTypeDTO> resp = new PagingResponse<InterventionTypeDTO>()
                {
                    Items = await items.Select(item => new InterventionTypeDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        Translate = item.InterventionTypeLanguages.Where(x => x.Language.LanguageCode == GetLanguage()).Select(x => x.Name).FirstOrDefault() ?? item.Name
                    }).ToListAsync(),
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

        public async Task<List<InterventionTypeDTO>?> GetListAsync(InterventionTypeFilter? args = null)
        {
            try
            {
                var items = FilterItems(args);

                if (items == null)
                {
                    return new List<InterventionTypeDTO>();
                }

                return await items.Select(item => new InterventionTypeDTO  
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    Translate = item.InterventionTypeLanguages.Where(x => x.Language.LanguageCode == GetLanguage()).Select(x => x.Name).FirstOrDefault() ?? item.Name
                   
                }).ToListAsync();
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public async Task<APIResponseMessage<InterventionTypeDTO>> PostAsync(InterventionType item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.InterventionTypes.Update(item);
                }
                else
                {
                    _context.InterventionTypes.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<InterventionTypeDTO>
                {
                    State = true,
                    Data = new InterventionTypeDTO 
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        
                    },
                    
                    Message = "Logo Event saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<InterventionTypeDTO>
                {
                    State = false,
                    Message = "Error saving InterventionType",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.InterventionTypes.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {

                _context.InterventionTypes.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<string> Translate(int id)
        {
            var item = await _context.InterventionTypes.FindAsync(id);
            return item?.InterventionTypeLanguages.Where(x => x.Language.LanguageCode == GetLanguage()).Select(x => x.Name).FirstOrDefault() ?? string.Empty;
        }

        private async Task<string?> GetCurrentUserId()
        {
            return await _permitsService.IdUser();
        }

        private string? GetLanguage()
        {
            var acceptLanguage = _httpContextAccessor.HttpContext?.Request.Headers["Accept-Language"].ToString();
            if (string.IsNullOrEmpty(acceptLanguage))
            {
                acceptLanguage = "it-IT"; // Default language
            }
            return acceptLanguage;
        }

        private IQueryable<InterventionType>? FilterItems(InterventionTypeFilter? args = null)
        {
            try
            {
                var items = _context.InterventionTypes.AsQueryable();
                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderByDescending(x => x.Name);

                

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
