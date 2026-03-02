


using CRM.Shared;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ILanguagesService: IDataService<Language, int, LanguageFilter, object>
    {
        Task<bool> SetIdLanguage(int id);

        Task<int?> GetIdLanguage();

        Task<string?> GetCodeLanguage();

        Task<bool> SetCodeLanguage(string code);

        
    }
}
