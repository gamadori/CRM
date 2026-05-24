using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    public class ProductCatalogAssetUploadRequest
    {
        public int IdProduct { get; set; }

        public string? Description { get; set; }

        public bool IncludeInCatalog { get; set; } = true;

        public List<AttachmentFile> Files { get; set; } = new();
    }
}
