using System;
using System.Collections.Generic;
using System.Linq;

namespace CRM.Shared.DTOs
{
    public class ProductCatalogFilter
    {
        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 24;

        public int? IdProductType { get; set; }

        public string? Search { get; set; }
    }

    public class ProductCatalogPageDTO
    {
        public List<ProductCatalogListItemDTO> Products { get; set; } = new();

        public List<ProductCatalogTypeDTO> ProductTypes { get; set; } = new();

        public int TotalCount { get; set; }

        public int PageNumber { get; set; }

        public int PageSize { get; set; }

        public int TotalPages { get; set; }
    }

    public class ProductCatalogTypeDTO
    {
        public int? IdProductType { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Count { get; set; }
    }

    public class ProductCatalogListItemDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Code { get; set; }

        public int? IdProductType { get; set; }

        public string ProductTypeName { get; set; } = string.Empty;

        public string? CompanyName { get; set; }

        public decimal Price { get; set; }

        public int? CoverAssetId { get; set; }

        public int? CoverAttachmentFileId { get; set; }

        public ProductCatalogMediaTypes? CoverMediaType { get; set; }

        public int ImagesCount { get; set; }

        public int DxfCount { get; set; }

        public int AttachmentsCount { get; set; }
    }

    public class ProductCatalogDetailDTO
    {
        public ProductCatalogListItemDTO Product { get; set; } = new();

        public List<ProductCatalogAssetDTO> Images { get; set; } = new();

        public List<ProductCatalogAssetDTO> Dxfs { get; set; } = new();

        public List<ProductCatalogAttachmentDTO> Attachments { get; set; } = new();
    }

    public class ProductCatalogAttachmentDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime? CreatedOn { get; set; }

        public List<ProductCatalogAttachmentFileDTO> Files { get; set; } = new();

        public double TotalSize => Files.Sum(x => x.Size);
    }

    public class ProductCatalogAttachmentFileDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? ContentType { get; set; }

        public double Size { get; set; }
    }
}
