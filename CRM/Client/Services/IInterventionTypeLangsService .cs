using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IInterventionTypeLangsService : IDataService<InterventionTypeLanguage, InterventionTypeLangDTO, int, InterventionTypeLangFilter, object>
    {
        Task<string?> GetFlagAsync(int id);
    }
}
