using CRM.Client.Models;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IGanttPhasesService
    {
        Task<List<GanttPhaseDTO>?> GetTreeAsync(int idGanttPlan);

        Task<APIResponseMessage<GanttPhaseDTO>> SaveAsync(GanttPhaseDTO dto);

        Task<bool> BulkSaveAsync(List<GanttPhaseDTO> dtos);

        Task<bool> DeleteAsync(int phaseId);

        Task<APIResponseMessage<GanttPhaseDependencyDTO>> AddDependencyAsync(GanttPhaseDependencyDTO dto);

        Task<bool> RemoveDependencyAsync(int dependencyId);
    }
}
