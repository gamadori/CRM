using CRM.Client.Models;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IGanttPhaseService
    {
        Task<List<GanttPhaseDTO>> GetTreeAsync(int idGanttPlan);
        Task<APIResponseMessage<GanttPhaseDTO>> SaveAsync(GanttPhaseDTO dto);
        Task<bool> DeleteAsync(int phaseId);
        Task<APIResponseMessage<GanttPhaseDependencyDTO>> AddDependencyAsync(GanttPhaseDependencyDTO dto);
        Task<bool> RemoveDependencyAsync(int dependencyId);
    }
}
