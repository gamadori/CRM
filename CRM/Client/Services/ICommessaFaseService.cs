using CRM.Client.Models;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ICommessaFaseService
    {
        Task<List<CommessaFaseDTO>> GetTreeAsync(int idCommessa);

        Task<APIResponseMessage<CommessaFaseDTO>> SaveAsync(CommessaFaseDTO dto);

        Task<bool> BulkSaveAsync(List<CommessaFaseDTO> dtos);

        Task<bool> DeleteAsync(int faseId);

        Task<APIResponseMessage<CommessaFaseDependencyDTO>> AddDependencyAsync(CommessaFaseDependencyDTO dto);

        Task<bool> RemoveDependencyAsync(int dependencyId);
    }
}
