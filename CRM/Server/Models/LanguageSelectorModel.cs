using CRM.Shared;

namespace CRM.Server.Models
{
    public class LanguageSelectorModel
    {
        public Language? Language { get; set; }

        public List<Language> Languages { get; set; } = new List<Language>();

       
    }
}
