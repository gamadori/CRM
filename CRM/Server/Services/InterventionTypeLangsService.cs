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
    public class InterventionTypeLangsService : IInterventionTypeLangsService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        public InterventionTypeLangsService(
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

        public async Task<InterventionTypeLangDTO?> GetItemAsync(int id)
        {
            
            
            var item = await _context.InterventionTypeLanguages
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            return item != null ? new InterventionTypeLangDTO()
            {
                Id = item.Id,
                Name = item.Name,
                IdLanguage = item.IdLanguage,
                IdInterventionType = item.IdInterventionType,
                Language = item.Language.Name,
                NameInterventionType = item.InterventionType.Name,  
                
            } : null;
        }

        public async Task<InterventionTypeLangDTO?> GetFirstAsync()
        {
            var acceptLanguage = GetLanguage();
            var item = await _context.InterventionTypeLanguages
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item != null ? new InterventionTypeLangDTO()
            {
                Id = item.Id,
                Name = item.Name,
                IdLanguage = item.IdLanguage,
                IdInterventionType = item.IdInterventionType,
                Language = item.Language.Name,
                NameInterventionType = item.InterventionType.Name,  
                
            } : null;
        }

        public async Task<PagingResponse<InterventionTypeLangDTO, object>?> GetSummaryAsync(InterventionTypeLangFilter? args)
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
                PagingResponse<InterventionTypeLangDTO, object> resp = new PagingResponse<InterventionTypeLangDTO, object>()
                {
                    Items = await items.Select(item => new InterventionTypeLangDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        IdLanguage = item.IdLanguage,
                        IdInterventionType = item.IdInterventionType,
                        Language = item.Language.Name,
                        NameInterventionType = item.InterventionType.Name,

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

        public async Task<PagingResponse<InterventionTypeLangDTO>?> GetPagingAsync(InterventionTypeLangFilter? args = null)
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
                PagingResponse<InterventionTypeLangDTO> resp = new PagingResponse<InterventionTypeLangDTO>()
                {
                    Items = await items.Select(item => new InterventionTypeLangDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        IdLanguage = item.IdLanguage,
                        IdInterventionType = item.IdInterventionType,
                        Language = item.Language.Name,
                        NameInterventionType = item.InterventionType.Name,
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

        public async Task<List<InterventionTypeLangDTO>?> GetListAsync(InterventionTypeLangFilter? args = null)
        {
            try
            {
                var items = FilterItems(args);

                if (items == null)
                {
                    return new List<InterventionTypeLangDTO>();
                }

                return await items.Select(item => new InterventionTypeLangDTO  
                {
                    Id = item.Id,
                    Name = item.Name,
                    IdLanguage = item.IdLanguage,
                    IdInterventionType = item.IdInterventionType,
                    Language = item.Language.Name,
                    NameInterventionType = item.InterventionType.Name,

                }).ToListAsync();
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public async Task<APIResponseMessage<InterventionTypeLangDTO>> PostAsync(InterventionTypeLanguage item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.InterventionTypeLanguages.Update(item);
                }
                else
                {
                    _context.InterventionTypeLanguages.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<InterventionTypeLangDTO>
                {
                    State = true,
                    Data = new InterventionTypeLangDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        IdLanguage = item.IdLanguage,
                        IdInterventionType = item.IdInterventionType,
                        Language = item.Language.Name,
                        NameInterventionType = item.InterventionType.Name,
                        
                    },
                    
                    Message = "Logo Event saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<InterventionTypeLangDTO>
                {
                    State = false,
                    Message = "Error saving InterventionTypeLanguage",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.InterventionTypeLanguages.FindAsync(id);
            if (item == null)
            {
                return false;
            }
            try
            {

                _context.InterventionTypeLanguages.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        [HttpGet("Flag")]
        public async Task<string?> GetFlagAsync(int id)
        {
            var acceptLanguage = GetLanguage();
            var language = await _context.Languages.FirstOrDefaultAsync(x => x.Id  == id);
            return language != null ? language.Flag : null;
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

        private IQueryable<InterventionTypeLanguage>? FilterItems(InterventionTypeLangFilter? args = null)
        {
            try
            {
                var items = _context.InterventionTypeLanguages.AsQueryable();
                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderByDescending(x => x.Name);

                
                if (args.IdInterventionType != null)
                {
                    items = items.Where(x => x.IdInterventionType == args.IdInterventionType);
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
