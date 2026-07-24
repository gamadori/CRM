using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IGanttPlansService
    {
        Task<List<GanttPlanDTO>?> GetListAsync(GanttPlanFilter? args = null);

        Task<GanttPlanDTO?> GetItemAsync(int id);

        Task<APIResponseMessage<GanttPlanDTO>> SaveAsync(GanttPlanDTO dto);

        Task<APIResponseMessage<GanttPlanDTO>> DeleteAsync(int id);
    }
}
