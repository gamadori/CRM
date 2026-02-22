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
    public class AttachmentsService : IAttachmentsService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;

        public AttachmentsService(
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

        public async Task<AttachmentDTO?> GetItemAsync(int id)
        {
            var item = await _context.Attachments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return item != null ? new AttachmentDTO
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                IdParent = item.IdParent,
                AttchmentType = item.AttchmentType,
                FolderId = item.FolderId,
                CreatedOn = item.CreatedOn,
                IdUser = item.IdUser,
                NameUser = item.User?.NameComplete,
                Folder = item.Folder?.Name
            } : null;
        }

        public async Task<AttachmentDTO?> GetFirstAsync()
        {
            var item = await _context.Attachments
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item != null ? new AttachmentDTO()
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                IdParent = item.IdParent,
                AttchmentType = item.AttchmentType,
                FolderId = item.FolderId,
                CreatedOn = item.CreatedOn,
                IdUser = item.IdUser,
                NameUser = item.User?.NameComplete,
                Folder = item.Folder?.Name
            } : null;
        }

       

        public async Task<PagingResponse<AttachmentDTO, object>?> GetSummaryAsync(AttachmentsFilter? args)
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
                PagingResponse<AttachmentDTO, object> resp = new PagingResponse<AttachmentDTO, object>()
                {
                    Items = await items.Select(item => new AttachmentDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        IdParent = item.IdParent,
                        AttchmentType = item.AttchmentType,
                        FolderId = item.FolderId,
                        CreatedOn = item.CreatedOn,
                        IdUser = item.IdUser,
                        NameUser = item.User!.NameComplete,
                        Folder = item.Folder!.Name

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

        public async Task<PagingResponse<AttachmentDTO>?> GetPagingAsync(AttachmentsFilter? args = null)
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
                PagingResponse<AttachmentDTO> resp = new PagingResponse<AttachmentDTO>()
                {
                    Items = await items.Select(item => new AttachmentDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        IdParent = item.IdParent,
                        AttchmentType = item.AttchmentType,
                        FolderId = item.FolderId,
                        CreatedOn = item.CreatedOn,
                        IdUser = item.IdUser,
                        NameUser = item.User!.NameComplete,
                        Folder = item.Folder!.Name

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

        public async Task<List<AttachmentDTO>?> GetListAsync(AttachmentsFilter? args = null)
        {
            try
            {
                var items = FilterItems(args);

                if (items == null)
                {
                    return new List<AttachmentDTO>();
                }

                return await items.Select(item => new AttachmentDTO
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    IdParent = item.IdParent,
                    AttchmentType = item.AttchmentType,
                    FolderId = item.FolderId,
                    CreatedOn = item.CreatedOn,
                    IdUser = item.IdUser,
                    NameUser = item.User!.NameComplete,
                    Folder = item.Folder!.Name
                   
                }).ToListAsync();
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public async Task<APIResponseMessage<AttachmentDTO>> PostAsync(Attachment  item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.Attachments.Update(item);
                }
                else
                {
                    _context.Attachments.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<AttachmentDTO>
                {
                    State = true,
                    Data = new AttachmentDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        IdParent = item.IdParent,
                        AttchmentType = item.AttchmentType,
                        FolderId = item.FolderId,
                        CreatedOn = item.CreatedOn,
                        IdUser = item.IdUser,
                        NameUser = item.User!.NameComplete,
                        Folder = item.Folder!.Name
                    },
                    Message = "Attachment saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<AttachmentDTO>
                {
                    State = false,
                    Message = "Error saving Attachment",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Attachments.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {

                _context.Attachments.Remove(item);
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

       
       
        private IQueryable<Attachment>? FilterItems(AttachmentsFilter? args = null)
        {
            try
            {
                var items = _context.Attachments.AsQueryable();
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
