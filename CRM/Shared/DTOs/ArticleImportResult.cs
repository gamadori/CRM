using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    public class ArticleImportResult
    {
        public bool Success { get; set; }
        public int SheetsProcessed { get; set; }
        public int RowsProcessed { get; set; }
        public int CompaniesCreated { get; set; }
        public int ArticlesCreated { get; set; }
        public int ArticlesSkipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
