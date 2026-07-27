using CRM.Client.Models;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ICommessaFaseService
    {
        Task<List<CommessaFaseDTO>> GetTreeAsync(int idCommessa);

        /// <summary>Singola fase con il codice della commessa, per mostrarla nel ticket.</summary>
        Task<CommessaFaseDTO?> GetItemAsync(int faseId);

        Task<APIResponseMessage<CommessaFaseDTO>> SaveAsync(CommessaFaseDTO dto);

        Task<bool> BulkSaveAsync(List<CommessaFaseDTO> dtos);

        Task<bool> DeleteAsync(int faseId);

        Task<APIResponseMessage<CommessaFaseDependencyDTO>> AddDependencyAsync(CommessaFaseDependencyDTO dto);

        Task<bool> RemoveDependencyAsync(int dependencyId);

        Task<APIResponseMessage<CommessaFaseTicketPlanDTO>> GenerateTicketFromPlanAsync(int ticketPlanId);
    }
}
