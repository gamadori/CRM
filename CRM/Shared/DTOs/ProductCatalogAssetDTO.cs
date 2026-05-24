using System;

namespace CRM.Shared.DTOs
{
    public class ProductCatalogAssetDTO
    {
        public int Id { get; set; }

        public int IdProduct { get; set; }

        public int IdAttachmentFile { get; set; }

        public int IdAttachment { get; set; }

        public ProductCatalogMediaTypes MediaType { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public int SortOrder { get; set; }

        public bool IsCover { get; set; }

        public bool IncludeInCatalog { get; set; }

        public DateTime CreatedOn { get; set; }

        public string? FileName { get; set; }

        public string? ContentType { get; set; }

        public double Size { get; set; }
    }
}
