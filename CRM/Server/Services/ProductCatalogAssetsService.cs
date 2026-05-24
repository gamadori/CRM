using CRM.Client.Models;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System.Linq.Dynamic.Core;

namespace CRM.Server.Services
{
    public class ProductCatalogAssetsService : IProductCatalogAssetsService
    {
        private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private readonly ApplicationDbContext _context;
        private readonly IArchiveService _archiveService;
        private readonly IPermitsService _permitsService;

        public ProductCatalogAssetsService(ApplicationDbContext context, IArchiveService archiveService, IPermitsService permitsService)
        {
            _context = context;
            _archiveService = archiveService;
            _permitsService = permitsService;
        }

        public async Task<ProductCatalogAssetDTO?> GetItemAsync(int id)
        {
            var item = await ProductCatalogQuery().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return item == null ? null : Map(item);
        }

        public async Task<PagingResponse<ProductCatalogAssetDTO>?> GetPagingAsync(ProductCatalogAssetFilter? args = null)
        {
            var items = FilterItems(args);
            var count = await items.CountAsync();

            if (args?.Skip != null && args.Top != null)
            {
                items = items.Skip(args.Skip.Value).Take(args.Top.Value);
            }

            var itemList = await items.ToListAsync();

            return new PagingResponse<ProductCatalogAssetDTO>
            {
                Items = itemList.Select(Map).ToList(),
                MetaData = new PagingHeaderModel
                {
                    TotalCount = count,
                    PageSize = args?.PageSize ?? 0
                },
                Total = string.Empty
            };
        }

        public async Task<List<ProductCatalogAssetDTO>?> GetListAsync(ProductCatalogAssetFilter? args = null)
        {
            var items = await FilterItems(args).ToListAsync();
            return items.Select(Map).ToList();
        }

        public async Task<APIResponseMessage<ProductCatalogAssetDTO>> PostAsync(ProductCatalogAsset item)
        {
            try
            {
                if (item.IsCover)
                {
                    await ClearCoverAsync(item.IdProduct, item.Id);
                }

                if (item.Id > 0)
                {
                    _context.ProductCatalogAssets.Update(item);
                }
                else
                {
                    _context.ProductCatalogAssets.Add(item);
                }

                await _context.SaveChangesAsync();

                var dto = await GetItemAsync(item.Id);
                return new APIResponseMessage<ProductCatalogAssetDTO>
                {
                    State = true,
                    Data = dto!,
                    Message = "Product catalog asset saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch
            {
                return new APIResponseMessage<ProductCatalogAssetDTO>
                {
                    State = false,
                    Message = "Error saving product catalog asset",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        public async Task<APIResponseMessage<List<ProductCatalogAssetDTO>>> UploadAsync(ProductCatalogAssetUploadRequest request)
        {
            try
            {
                var productExists = await _context.Products.AnyAsync(x => x.Id == request.IdProduct);
                if (!productExists)
                {
                    return Error("Product not found", System.Net.HttpStatusCode.NotFound);
                }

                if (request.Files == null || request.Files.Count == 0)
                {
                    return Error("No files selected", System.Net.HttpStatusCode.BadRequest);
                }

                var idUser = await _permitsService.IdUser();
                var sortOrder = await _context.ProductCatalogAssets
                    .Where(x => x.IdProduct == request.IdProduct)
                    .Select(x => (int?)x.SortOrder)
                    .MaxAsync() ?? 0;
                var hasCover = await _context.ProductCatalogAssets.AnyAsync(x => x.IdProduct == request.IdProduct && x.IsCover);
                var created = new List<ProductCatalogAssetDTO>();

                foreach (var file in request.Files)
                {
                    var mediaType = GetMediaType(file.Name);
                    if (mediaType == null)
                    {
                        return Error($"Unsupported catalog file type: {file.Name}", System.Net.HttpStatusCode.BadRequest);
                    }

                    var bytes = Convert.FromBase64String(file.Content);
                    var attachment = new Attachment
                    {
                        IdParent = request.IdProduct,
                        AttchmentType = AttachmentTypes.ProductCatalog,
                        Name = Path.GetFileNameWithoutExtension(file.Name),
                        Description = request.Description,
                        CreatedOn = DateTime.Now,
                        IdUser = idUser,
                        Visibility = AttachmentVisibilities.Public,
                        Files = new List<AttachmentFile>
                        {
                            new()
                            {
                                Name = file.Name,
                                Content = file.Content,
                                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                                    ? MimeTypes.GetMimeType(file.Name)
                                    : file.ContentType,
                                FileType = mediaType.Value.ToString(),
                                Link = string.Empty,
                                Size = bytes.Length
                            }
                        }
                    };

                    _context.Attachments.Add(attachment);
                    await _context.SaveChangesAsync();

                    var attachmentFile = attachment.Files.First();
                    _archiveService.SaveAttachments(attachmentFile.Id, Path.GetExtension(attachmentFile.Name), attachmentFile.Content);

                    var asset = new ProductCatalogAsset
                    {
                        IdProduct = request.IdProduct,
                        IdAttachmentFile = attachmentFile.Id,
                        MediaType = mediaType.Value,
                        Title = Path.GetFileNameWithoutExtension(file.Name),
                        Description = request.Description,
                        IncludeInCatalog = request.IncludeInCatalog,
                        SortOrder = ++sortOrder,
                        IsCover = !hasCover,
                        CreatedOn = DateTime.Now
                    };

                    if (asset.IsCover)
                    {
                        hasCover = true;
                    }

                    _context.ProductCatalogAssets.Add(asset);
                    await _context.SaveChangesAsync();

                    var dto = await GetItemAsync(asset.Id);
                    if (dto != null)
                    {
                        created.Add(dto);
                    }
                }

                return new APIResponseMessage<List<ProductCatalogAssetDTO>>
                {
                    State = true,
                    Data = created,
                    Message = "Product catalog assets uploaded successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (FormatException)
            {
                return Error("Invalid file content", System.Net.HttpStatusCode.BadRequest);
            }
            catch
            {
                return Error("Error uploading product catalog assets", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.ProductCatalogAssets
                .Include(x => x.AttachmentFile)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (item == null)
            {
                return false;
            }

            var idAttachment = item.AttachmentFile?.IdAttachment;
            _context.ProductCatalogAssets.Remove(item);

            if (item.AttachmentFile != null)
            {
                _context.AttachmentFiles.Remove(item.AttachmentFile);
            }

            if (idAttachment != null)
            {
                var attachment = await _context.Attachments.FindAsync(idAttachment.Value);
                if (attachment != null)
                {
                    _context.Attachments.Remove(attachment);
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetCoverAsync(int id)
        {
            var item = await _context.ProductCatalogAssets.FindAsync(id);
            if (item == null)
            {
                return false;
            }

            await ClearCoverAsync(item.IdProduct, item.Id);
            item.IsCover = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(byte[] Bytes, string ContentType, string FileName)> DownloadFileAsync(int id)
        {
            var item = await ProductCatalogQuery().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (item?.AttachmentFile == null)
            {
                return (Array.Empty<byte>(), string.Empty, string.Empty);
            }

            return (
                _archiveService.GetAttachment(item.AttachmentFile.Id, item.AttachmentFile.Name),
                MimeTypes.GetMimeType(item.AttachmentFile.Name),
                item.AttachmentFile.Name);
        }

        private IQueryable<ProductCatalogAsset> FilterItems(ProductCatalogAssetFilter? args)
        {
            var items = ProductCatalogQuery().AsNoTracking();

            if (!string.IsNullOrWhiteSpace(args?.OrderBy))
            {
                items = items.OrderBy(args.OrderBy);
            }
            else
            {
                items = items.OrderBy(x => x.SortOrder).ThenBy(x => x.Id);
            }

            if (args?.IdProduct != null)
            {
                items = items.Where(x => x.IdProduct == args.IdProduct);
            }

            if (args?.MediaType != null)
            {
                items = items.Where(x => x.MediaType == args.MediaType);
            }

            if (args?.IncludeInCatalog != null)
            {
                items = items.Where(x => x.IncludeInCatalog == args.IncludeInCatalog);
            }

            if (!string.IsNullOrWhiteSpace(args?.Title))
            {
                items = items.Where(x => x.Title != null && x.Title.Contains(args.Title));
            }

            if (!string.IsNullOrWhiteSpace(args?.Filter))
            {
                items = items.Where(args.Filter);
            }

            return items;
        }

        private IQueryable<ProductCatalogAsset> ProductCatalogQuery()
        {
            return _context.ProductCatalogAssets
                .Include(x => x.AttachmentFile);
        }

        private static ProductCatalogAssetDTO Map(ProductCatalogAsset item)
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

        private static ProductCatalogMediaTypes? GetMediaType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (ImageExtensions.Contains(extension))
            {
                return ProductCatalogMediaTypes.Image;
            }

            if (extension == ".dxf")
            {
                return ProductCatalogMediaTypes.Dxf;
            }

            return null;
        }

        private async Task ClearCoverAsync(int idProduct, int exceptId)
        {
            var covers = await _context.ProductCatalogAssets
                .Where(x => x.IdProduct == idProduct && x.Id != exceptId && x.IsCover)
                .ToListAsync();
            foreach (var cover in covers)
            {
                cover.IsCover = false;
            }
        }

        private static APIResponseMessage<List<ProductCatalogAssetDTO>> Error(string message, System.Net.HttpStatusCode code)
        {
            return new APIResponseMessage<List<ProductCatalogAssetDTO>>
            {
                State = false,
                Message = message,
                Code = code,
                Data = new List<ProductCatalogAssetDTO>()
            };
        }
    }
}
