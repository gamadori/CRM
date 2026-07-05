using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IActivityService
    {
        Task<List<ActivityDTO>> GetByEntityAsync(ActivityEntityType entityType, int entityId);
        Task<List<ActivityDTO>> GetMyAgendaAsync(ActivityFilter? filter = null);
        Task<APIResponseMessage<ActivityDTO>> PostAsync(Activity item);
        Task<APIResponseMessage<ActivityDTO>> CompleteAsync(int id);
        Task<bool> DeleteAsync(int id);
    }
}
