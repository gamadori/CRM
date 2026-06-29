using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    public class ProductImportResult
    {
        public bool Success { get; set; }
        public int ProductTypesCreated { get; set; }
        public int ProductsCreated { get; set; }
        public int ProductsUpdated { get; set; }
        public int RowsSkipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
