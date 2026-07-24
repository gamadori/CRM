using CRM.Client.Models;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface ICommessaFasiService
    {
        Task<List<CommessaFaseDTO>?> GetTreeAsync(int idCommessa);

        Task<APIResponseMessage<CommessaFaseDTO>> SaveAsync(CommessaFaseDTO dto);

        Task<bool> BulkSaveAsync(List<CommessaFaseDTO> dtos);

        Task<bool> DeleteAsync(int faseId);

        Task<APIResponseMessage<CommessaFaseDependencyDTO>> AddDependencyAsync(CommessaFaseDependencyDTO dto);

        Task<bool> RemoveDependencyAsync(int dependencyId);

        /// <summary>Ricalcola l'avanzamento di una fase dai suoi ticket (chiusi/totali) e propaga
        /// alla commessa. No-op se faseId è null.</summary>
        Task RecomputeFaseProgressAsync(int? faseId);
    }
}
