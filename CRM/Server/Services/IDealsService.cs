using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IDealsService
    {
        Task<DealDTO?> GetItemAsync(int id);
        Task<DealDTO?> GetFirstAsync();
        Task<PagingResponse<DealDTO, decimal>?> GetSummaryAsync(DealFilter? args);
        Task<CommercialForecastDTO?> GetForecastAsync(DealForecastFilter? args);
        Task<PagingResponse<DealDTO>?> GetPagingAsync(DealFilter? args = null);
        Task<List<DealDTO>?> GetListAsync(DealFilter? args = null);
        Task<APIResponseMessage<DealDTO>> PostAsync(Deal item);
        Task<bool> DeleteAsync(int id);
    }
}
