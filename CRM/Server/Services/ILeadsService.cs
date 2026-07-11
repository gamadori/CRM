using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface ILeadsService
    {
        Task<LeadDTO?> GetItemAsync(int id);
        Task<PagingResponse<LeadDTO, decimal>?> GetSummaryAsync(LeadFilter? args);
        Task<List<LeadDTO>?> GetListAsync(LeadFilter? args = null);
        Task<APIResponseMessage<LeadDTO>> PostAsync(Lead item);
        Task<APIResponseMessage<DealDTO>> ConvertAsync(int id, ConvertLeadRequest request);
        Task<bool> DeleteAsync(int id);
    }
}
