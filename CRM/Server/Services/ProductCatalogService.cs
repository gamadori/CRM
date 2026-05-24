using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    public class ProductCatalogService : IProductCatalogService
    {
        private const int DefaultPageSize = 24;
        private const int MaxPageSize = 60;

        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;

        public ProductCatalogService(ApplicationDbContext context, IPermitsService permitsService)
        {
            _context = context;
            _permitsService = permitsService;
        }

        public async Task<ProductCatalogPageDTO> GetPageAsync(ProductCatalogFilter? filter)
        {
            filter ??= new ProductCatalogFilter();

            var pageNumber = Math.Max(1, filter.PageNumber);
            var pageSize = Math.Clamp(filter.PageSize <= 0 ? DefaultPageSize : filter.PageSize, 1, MaxPageSize);
            var baseProducts = await BuildCatalogProductsQuery(filter.Search);

            var productTypes = await baseProducts
                .GroupBy(x => new
                {
                    x.IdProductType,
                    ProductTypeName = x.ProductType != null ? x.ProductType.Name : string.Empty
                })
                .Select(x => new ProductCatalogTypeDTO
                {
                    IdProductType = x.Key.IdProductType,
                    Name = x.Key.ProductTypeName ?? string.Empty,
                    Count = x.Count()
                })
                .OrderBy(x => x.Name)
                .ToListAsync();
            foreach (var type in productTypes)
            {
                type.Name = string.IsNullOrWhiteSpace(type.Name) ? "Senza tipo" : type.Name;
            }

            var filteredProducts = baseProducts;
            if (filter.IdProductType != null)
            {
                filteredProducts = filteredProducts.Where(x => x.IdProductType == filter.IdProductType.Value);
            }

            var totalCount = await filteredProducts.CountAsync();
            var products = await filteredProducts
                .OrderBy(x => x.ProductType != null ? x.ProductType.Name : string.Empty)
                .ThenBy(x => x.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ProductCatalogListItemDTO
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    Code = x.Code,
                    IdProductType = x.IdProductType,
                    ProductTypeName = x.ProductType != null ? x.ProductType.Name : string.Empty,
                    CompanyName = x.Company != null ? x.Company.RagioneSociale : string.Empty,
                    Price = x.Price
                })
                .ToListAsync();

            await EnrichProductsAsync(products);

            return new ProductCatalogPageDTO
            {
                Products = products,
                ProductTypes = productTypes,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<ProductCatalogDetailDTO?> GetDetailsAsync(int idProduct)
        {
            var productsQuery = await BuildCatalogProductsQuery(null);
            var product = await productsQuery
                .Where(x => x.Id == idProduct)
                .Select(x => new ProductCatalogListItemDTO
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    Code = x.Code,
                    IdProductType = x.IdProductType,
                    ProductTypeName = x.ProductType != null ? x.ProductType.Name : string.Empty,
                    CompanyName = x.Company != null ? x.Company.RagioneSociale : string.Empty,
                    Price = x.Price
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return null;
            }

            await EnrichProductsAsync(new List<ProductCatalogListItemDTO> { product });

            var assetEntities = await _context.ProductCatalogAssets
                .AsNoTracking()
                .Include(x => x.AttachmentFile)
                .Where(x => x.IdProduct == idProduct && x.IncludeInCatalog)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToListAsync();
            var assets = assetEntities.Select(MapAsset).ToList();

            var visibleAttachments = await VisibleProductAttachments();
            var attachments = await visibleAttachments
                .Where(x => x.IdParent == idProduct)
                .OrderByDescending(x => x.CreatedOn)
                .ThenBy(x => x.Name)
                .Select(x => new ProductCatalogAttachmentDTO
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    CreatedOn = x.CreatedOn,
                    Files = x.Files
                        .OrderBy(f => f.Name)
                        .Select(f => new ProductCatalogAttachmentFileDTO
                        {
                            Id = f.Id,
                            Name = f.Name,
                            ContentType = f.ContentType,
                            Size = f.Size
                        })
                        .ToList()
                })
                .ToListAsync();

            return new ProductCatalogDetailDTO
            {
                Product = product,
                Images = assets.Where(x => x.MediaType == ProductCatalogMediaTypes.Image)
                    .OrderByDescending(x => x.IsCover)
                    .ThenBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .ToList(),
                Dxfs = assets.Where(x => x.MediaType == ProductCatalogMediaTypes.Dxf)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .ToList(),
                Attachments = attachments
            };
        }

        private async Task<IQueryable<Product>> BuildCatalogProductsQuery(string? search)
        {
            var products = _context.Products
                .AsNoTracking()
                .Where(x => _context.ProductCatalogAssets.Any(a => a.IdProduct == x.Id && a.IncludeInCatalog));

            if (await _permitsService.IsClient())
            {
                var idCompany = await _permitsService.GetIdCompany();
                products = products.Where(x => x.Articles.Any(y => y.IdCompany == idCompany));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();
                products = products.Where(x =>
                    x.Name.Contains(value) ||
                    (x.Code != null && x.Code.Contains(value)) ||
                    (x.Description != null && x.Description.Contains(value)) ||
                    (x.ProductType != null && x.ProductType.Name.Contains(value)));
            }

            return products;
        }

        private async Task EnrichProductsAsync(List<ProductCatalogListItemDTO> products)
        {
            if (!products.Any())
            {
                return;
            }

            var productIds = products.Select(x => x.Id).ToList();
            var assets = await _context.ProductCatalogAssets
                .AsNoTracking()
                .Where(x => productIds.Contains(x.IdProduct) && x.IncludeInCatalog)
                .Select(x => new
                {
                    x.Id,
                    x.IdProduct,
                    x.IdAttachmentFile,
                    x.MediaType,
                    x.IsCover,
                    x.SortOrder
                })
                .ToListAsync();

            var visibleAttachments = await VisibleProductAttachments();
            var attachmentCounts = await visibleAttachments
                .Where(x => productIds.Contains(x.IdParent))
                .GroupBy(x => x.IdParent)
                .Select(x => new { IdProduct = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.IdProduct, x => x.Count);

            foreach (var product in products)
            {
                var productAssets = assets.Where(x => x.IdProduct == product.Id).ToList();
                product.ImagesCount = productAssets.Count(x => x.MediaType == ProductCatalogMediaTypes.Image);
                product.DxfCount = productAssets.Count(x => x.MediaType == ProductCatalogMediaTypes.Dxf);
                var cover = productAssets
                    .OrderByDescending(x => x.IsCover)
                    .ThenBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .FirstOrDefault();
                product.CoverAssetId = cover?.Id;
                product.CoverAttachmentFileId = cover?.IdAttachmentFile;
                product.CoverMediaType = cover?.MediaType;
                product.AttachmentsCount = attachmentCounts.TryGetValue(product.Id, out var count) ? count : 0;
                product.ProductTypeName = string.IsNullOrWhiteSpace(product.ProductTypeName) ? "Senza tipo" : product.ProductTypeName;
            }
        }

        private async Task<IQueryable<Attachment>> VisibleProductAttachments()
        {
            IQueryable<Attachment> attachments = _context.Attachments
                .AsNoTracking()
                .Include(x => x.Files)
                .Where(x => x.AttchmentType == AttachmentTypes.Product && x.Visibility == AttachmentVisibilities.Public);

            var canSeePrivate = await _permitsService.BelongsToHeadQuarter()
                || await _permitsService.BelongsToMainCompany();

            if (!canSeePrivate)
            {
                attachments = attachments.Where(x => x.Visibility == AttachmentVisibilities.Public);
            }

            return attachments;
        }

        private static ProductCatalogAssetDTO MapAsset(ProductCatalogAsset item)
        {
            return new ProductCatalogAssetDTO
            {
                Id = item.Id,
                IdProduct = item.IdProduct,
                IdAttachmentFile = item.IdAttachmentFile,
                IdAttachment = item.AttachmentFile?.IdAttachment ?? 0,
                MediaType = item.MediaType,
                Title = item.Title,
                Description = item.Description,
                SortOrder = item.SortOrder,
                IsCover = item.IsCover,
                IncludeInCatalog = item.IncludeInCatalog,
                CreatedOn = item.CreatedOn,
                FileName = item.AttachmentFile?.Name,
                ContentType = item.AttachmentFile?.ContentType,
                Size = item.AttachmentFile?.Size ?? 0
            };
        }
    }
}
