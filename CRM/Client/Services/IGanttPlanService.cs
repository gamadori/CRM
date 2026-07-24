using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IGanttPlanService
    {
        Task<List<GanttPlanDTO>> GetListAsync(GanttPlanFilter? args = null);

        Task<GanttPlanDTO?> GetItemAsync(int id);

        Task<APIResponseMessage<GanttPlanDTO>> SaveAsync(GanttPlanDTO dto);

        Task<APIResponseMessage<GanttPlanDTO>> DeleteAsync(int id);
    }
}
