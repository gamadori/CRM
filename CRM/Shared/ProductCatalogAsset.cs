using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    public enum ProductCatalogMediaTypes
    {
        Image,
        Dxf
    }

    public class ProductCatalogAsset
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Product))]
        public int IdProduct { get; set; }

        [ForeignKey(nameof(AttachmentFile))]
        public int IdAttachmentFile { get; set; }

        public ProductCatalogMediaTypes MediaType { get; set; }

        [MaxLength(150)]
        public string? Title { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public int SortOrder { get; set; }

        public bool IsCover { get; set; }

        public bool IncludeInCatalog { get; set; } = true;

        public DateTime CreatedOn { get; set; } = DateTime.Now;

        public Product? Product { get; set; }

        public AttachmentFile? AttachmentFile { get; set; }
    }

    public class ProductCatalogAssetFilter : PagingParameterModel
    {
        public int? IdProduct { get; set; }

        public ProductCatalogMediaTypes? MediaType { get; set; }

        public bool? IncludeInCatalog { get; set; }

        public string? Title { get; set; }
    }
}
