using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IFolderLanguagesService : IDataService<FolderLanguage, FolderLanguageDTO, int, FolderLanguageFilter, object>
    {
    }
}
