using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IOrdersService
    {
        Task<OrderDTO?> GetItemAsync(int id);
        Task<PagingResponse<OrderDTO, decimal>?> GetSummaryAsync(OrderFilter? args);
        Task<List<OrderDTO>?> GetListAsync(OrderFilter? args = null);
        Task<APIResponseMessage<OrderDTO>> PostAsync(Order item);
        Task<APIResponseMessage<OrderDTO>> CreateFromQuoteAsync(int quoteId);
        Task<APIResponseMessage<OrderDTO>> ChangeStateAsync(int id, OrderStates state);
        Task<(byte[] Bytes, string FileName)?> GeneratePdfAsync(int id);
        Task<bool> DeleteAsync(int id);
    }
}
