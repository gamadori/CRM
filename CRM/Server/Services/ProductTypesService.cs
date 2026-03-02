using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Controllers;
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
    /// Implementazione server del servizio Products.
    /// Accede direttamente al database.
    /// </summary>
    public class ProductTypesService : IProductTypesService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        
        public ProductTypesService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor,
            IPermitsService permitsService, ILogEventService logEventService)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
            _logEventService = logEventService;
            
        }

        public async Task<ProductTypeDTO?> GetItemAsync(int id)
        {
            var item = await _context.ProductTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return item != null ? new ProductTypeDTO
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
               
                

            } : null;
        }

        public async Task<ProductTypeDTO?> GetFirstAsync()
        {
           

            var item = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item != null ? new ProductTypeDTO()
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
               
            } : null;
        }

       

        public async Task<PagingResponse<ProductTypeDTO, object>?> GetSummaryAsync(ProductTypeFilter? args)
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
                PagingResponse<ProductTypeDTO, object> resp = new PagingResponse<ProductTypeDTO, object>()
                {
                    Items = await items.Select(item => new ProductTypeDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        

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

        public async Task<PagingResponse<ProductTypeDTO>?> GetPagingAsync(ProductTypeFilter? args = null)
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
                PagingResponse<ProductTypeDTO> resp = new PagingResponse<ProductTypeDTO>()
                {
                    Items = await items.Select(item => new ProductTypeDTO
                    {
                        Id = item.Id,
                        Name = item.Name        ,
                        Description = item.Description,
                       

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

        public async Task<List<ProductTypeDTO>?> GetListAsync(ProductTypeFilter? args = null)
        {
            try
            {
                var items = FilterItems(args);

                if (items == null)
                {
                    return new List<ProductTypeDTO>();
                }

                return await items.Select(item => new ProductTypeDTO
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


        public async Task<APIResponseMessage<ProductTypeDTO>> PostAsync(ProductType  item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.ProductTypes.Update(item);
                }
                else
                {
                    _context.ProductTypes.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<ProductTypeDTO>
                {
                    State = true,
                    Data = new ProductTypeDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        
                    },
                    Message = "Product saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<ProductTypeDTO>
                {
                    State = false,
                    Message = "Error saving Product Type",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.ProductTypes.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {

                _context.ProductTypes.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsTypesController), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

       
       
        private IQueryable<ProductType>? FilterItems(ProductTypeFilter? args = null)
        {
            try
            {
                var items = _context.ProductTypes.AsQueryable();
                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderByDescending(x => x.Name);

                if (args?.Name != null)
                {
                    items = items.Where(x => x.Name.Contains(args.Name));
                }

                if (args?.Description != null)
                {
                    items = items.Where(x => x.Description.Contains(args.Description));
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
