using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IInterventionTypesService : IDataService<InterventionType, InterventionTypeDTO, int, InterventionTypeFilter, object>
    {
        Task<string> Translate(int id);
    }
}
