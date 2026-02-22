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
    public class FoldersService : IFoldersService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        public FoldersService(
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

        public async Task<FolderDTO?> GetItemAsync(int id)
        {
            var item = await _context.Folders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return item != null ? new FolderDTO
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description  
                
            } : null;
        }

        public async Task<FolderDTO?> GetFirstAsync()
        {
            var item = await _context.Folders
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item != null ? new FolderDTO()
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description
            } : null;
        }

       

        public async Task<PagingResponse<FolderDTO, object>?> GetSummaryAsync(FolderFilter? args)
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
                PagingResponse<FolderDTO, object> resp = new PagingResponse<FolderDTO, object>()
                {
                    Items = await items.Select(item => new FolderDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description
                       
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

        public async Task<PagingResponse<FolderDTO>?> GetPagingAsync(FolderFilter? args = null)
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
                PagingResponse<FolderDTO> resp = new PagingResponse<FolderDTO>()
                {
                    Items = await items.Select(item => new FolderDTO
                    {
                        Id = item.Id,
                        Name = item.Name        ,
                        Description = item.Description
                       
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

        public async Task<List<FolderDTO>?> GetListAsync(FolderFilter? args = null)
        {
            try
            {
                var items = FilterItems(args);

                if (items == null)
                {
                    return new List<FolderDTO>();
                }

                return await items.Select(item => new FolderDTO
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    
                   
                }).ToListAsync();
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public async Task<APIResponseMessage<FolderDTO>> PostAsync(Folder  item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.Folders.Update(item);
                }
                else
                {
                    _context.Folders.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<FolderDTO>
                {
                    State = true,
                    Data = new FolderDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        
                    },
                    Message = "Folder Event saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<FolderDTO>
                {
                    State = false,
                    Message = "Error saving Folder",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Folders.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {

                _context.Folders.Remove(item);
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

       
       
        private IQueryable<Folder>? FilterItems(FolderFilter? args = null)
        {
            try
            {
                var items = _context.Folders.AsQueryable();
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
