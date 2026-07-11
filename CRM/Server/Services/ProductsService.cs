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
    /// Implementazione server del servizio Products.
    /// Accede direttamente al database.
    /// </summary>
    public class ProductsService : IProductsService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        
        public ProductsService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor,
            IPermitsService permitsService,
            ILogEventService logEventService)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        public async Task<ProductDTO?> GetItemAsync(int id)
        {
            var item = await _context.Products
                .Include(x => x.ProductType)
                .Include(x => x.Company)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return item.ToDTO();
            
        }

        public async Task<ProductDTO?> GetFirstAsync()
        {

            var item = await _context.Products
                .Include(x => x.ProductType)
                .Include(x => x.Company)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item.ToDTO();
        }

       

        public async Task<PagingResponse<ProductDTO, object>?> GetSummaryAsync(ProductFilter? args)
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
                PagingResponse<ProductDTO, object> resp = new PagingResponse<ProductDTO, object>()
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsService), nameof(GetSummaryAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<PagingResponse<ProductDTO>?> GetPagingAsync(ProductFilter? args = null)
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
                PagingResponse<ProductDTO> resp = new PagingResponse<ProductDTO>()
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsService), nameof(GetPagingAsync), EventsTypes.Error, ex);

                return null;
            }
        }

        public async Task<List<ProductDTO>?> GetListAsync(ProductFilter? args = null)
        {
            try
            {
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new List<ProductDTO>();
                }

                return await items.Select(item => item.ToDTO()).ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }


        public async Task<APIResponseMessage<ProductDTO>> PostAsync(Product  item)
        {
            try
            {

                if (item.Id > 0)
                {
                    var existing = await _context.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.Id);
                    if (existing != null)
                    {
                        item.IsArchived = existing.IsArchived;
                        item.ArchivedAt = existing.ArchivedAt;
                        item.ArchivedReason = existing.ArchivedReason;
                    }

                    _context.Products.Update(item);
                }
                else
                {
                    _context.Products.Add(item);
                }
                await _context.SaveChangesAsync();

                return new APIResponseMessage<ProductDTO>
                {
                    State = true,
                    Data = item.ToDTO(),
                    
                    Message = "Product saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsService), nameof(PostAsync), EventsTypes.Error, ex);

                return new APIResponseMessage<ProductDTO>
                {
                    State = false,
                    Message = "Error saving Product",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Products.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {
                if (await HasProductReferencesAsync(id))
                {
                    item.IsArchived = true;
                    item.ArchivedAt = DateTime.UtcNow;
                    item.ArchivedReason = "Archiviato automaticamente perche' il prodotto ha relazioni storiche.";
                }
                else
                {
                    _context.Products.Remove(item);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        private async Task<bool> HasProductReferencesAsync(int id)
        {
            return await _context.Articles.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.Tickets.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.TicketInterventionArticles.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.MachineBackups.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.Attachments.AsNoTracking().AnyAsync(x => x.AttchmentType == AttachmentTypes.Product && x.IdParent == id)
                || await _context.Projects.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.QuoteRows.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.OrderRows.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.InvoiceRows.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.PriceListItems.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.ProductAccessoryTypes.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.ProductCatalogAssets.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.ProductKnowledge.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.LeadProductInterests.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.DealProductInterests.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.ArticleLicenseFeatureDefs.AsNoTracking().AnyAsync(x => x.IdProduct == id)
                || await _context.Products.AsNoTracking().AnyAsync(x => x.Parents.Any(parent => parent.Id == id) || x.Childs.Any(child => child.Id == id));
        }

       
       
        private async Task<IQueryable<Product>?> FilterItems(ProductFilter? args = null)
        {
            try
            {
                var items = _context.Products.Include(x=>x.ProductType).Include(x=>x.Company).AsQueryable();

                if (args?.IncludeArchived != true)
                {
                    items = items.Where(x => !x.IsArchived);
                }

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
                if (await _permitsService.IsClient())
                {
                    int? idCompany = await _permitsService.GetIdCompany();
                    items = items.Where(x => x.Articles.Where(y => y.IdCompany == idCompany).Any());
                }

                if (args?.IdParent != null)
                {
                    items = items.Where(x => x.Parents.Where(x => x.Id == args.IdParent).Any());
                }


               
                if (args?.Code != null)
                {
                    items = items.Where(x => x.Code!.Contains(args.Code));
                }
                if (args?.Filter != null && args.Filter.Any())
                {
                    // I nomi delle colonne del grid sono quelli del DTO; qui si filtra sull'entità Product,
                    // quindi vanno tradotti nei path di navigazione corrispondenti.
                    var filter = args.Filter
                        .Replace("ProductTypeName", "ProductType.Name")
                        .Replace("CompanyName", "Company.RagioneSociale");
                    items = items.Where(filter);
                }

                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsService), nameof(FilterItems), EventsTypes.Error, ex);

                return null;
            }
        }
    }
}
