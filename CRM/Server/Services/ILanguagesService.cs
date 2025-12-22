using CRM.Server.Models;

namespace CRM.Server.Services
{
    public interface ILanguagesService
    {
        Task<int?> GetIdLanguage();
        
    }
}
