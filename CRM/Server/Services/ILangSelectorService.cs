using CRM.Server.Models;

namespace CRM.Server.Services
{
    public interface ILangSelectorService
    {
        Task<LanguageSelectorModel> OnGetAsync(string? lang);
    }
}
