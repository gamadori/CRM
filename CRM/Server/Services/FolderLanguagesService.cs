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
    public class FolderLanguagesService : IFolderLanguagesService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        public FolderLanguagesService(
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

        public async Task<FolderLanguageDTO?> GetItemAsync(int id)
        {
            var item = await _context.FolderLanguages
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return item != null ? new FolderLanguageDTO
            {
                Id = item.Id,
                IdFolder = item.FolderId,
                IdLanguage = item.LanguageId,
                Name = item.Name
            } : null;
        }

        public async Task<FolderLanguageDTO?> GetFirstAsync()
        {
            var item = await _context.FolderLanguages
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item != null ? new FolderLanguageDTO()
            {
                Id = item.Id,
                IdFolder = item.FolderId,
                IdLanguage = item.LanguageId,
                Name = item.Name,
                Language = item.Language != null ? item.Language.Name : ""
            } : null;
        }

       

        public async Task<PagingResponse<FolderLanguageDTO, object>?> GetSummaryAsync(FolderLanguageFilter? args)
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
                PagingResponse<FolderLanguageDTO, object> resp = new PagingResponse<FolderLanguageDTO, object>()
                {
                    Items = await items.Select(item => new FolderLanguageDTO
                    {
                        Id = item.Id,
                        IdFolder = item.FolderId,
                        IdLanguage = item.LanguageId,
                        Name = item.Name,
                        Language = item.Language != null ? item.Language.Name : ""

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

        public async Task<PagingResponse<FolderLanguageDTO>?> GetPagingAsync(FolderLanguageFilter? args = null)
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
                PagingResponse<FolderLanguageDTO> resp = new PagingResponse<FolderLanguageDTO>()
                {
                    Items = await items.Select(item => new FolderLanguageDTO
                    {
                        Id = item.Id,
                        IdFolder = item.FolderId,
                        IdLanguage = item.LanguageId,
                        Name = item.Name,
                        Language = item.Language != null ? item.Language.Name : ""

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

        public async Task<List<FolderLanguageDTO>?> GetListAsync(FolderLanguageFilter? args = null)
        {
            try
            {
                var items = FilterItems(args);

                if (items == null)
                {
                    return new List<FolderLanguageDTO>();
                }

                return await items.Select(item => new FolderLanguageDTO
                {
                    Id = item.Id,
                    IdFolder = item.FolderId,
                    IdLanguage = item.LanguageId,   
                    Name = item.Name,
                    Language = item.Language != null ? item.Language.Name : ""


                }).ToListAsync();
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public async Task<APIResponseMessage<FolderLanguageDTO>> PostAsync(FolderLanguage  item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.FolderLanguages.Update(item);
                }
                else
                {
                    _context.FolderLanguages.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<FolderLanguageDTO>
                {
                    State = true,
                    Data = new FolderLanguageDTO
                    {
                        Id = item.Id,
                        IdFolder = item.FolderId,
                        IdLanguage = item.LanguageId,
                        Name = item.Name,
                        Language = item.Language != null ? item.Language.Name : ""
                        
                    },
                    Message = "Folder Event saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<FolderLanguageDTO>
                {
                    State = false,
                    Message = "Error saving Folder Language",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.FolderLanguages.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {

                _context.FolderLanguages.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<string?> GetCurrentUserId()
        {
            return await _permitsService.IdUser();
        }

       
       
        private IQueryable<FolderLanguage>? FilterItems(FolderLanguageFilter? args = null)
        {
            try
            {
                var items = _context.FolderLanguages.AsQueryable();
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
