using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IActivitiesService
    {
        Task<List<ActivityDTO>?> GetByEntityAsync(ActivityEntityType entityType, int entityId);
        Task<List<ActivityDTO>?> GetMyAgendaAsync(ActivityFilter? args);
        Task<ActivityDTO?> GetItemAsync(int id);
        Task<APIResponseMessage<ActivityDTO>> PostAsync(Activity item);
        Task<APIResponseMessage<ActivityDTO>> CompleteAsync(int id);
        Task<bool> DeleteAsync(int id);
    }
}
