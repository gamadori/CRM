using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    public class CompanyTreeNodeDTO
    {
        public int Id { get; set; }
        public string RagioneSociale { get; set; }
        public CompanyTypes CompanyType { get; set; }
        public string? Citta { get; set; }
        public string? Email { get; set; }
        public List<CompanyTreeNodeDTO> Children { get; set; } = new();
    }
}
