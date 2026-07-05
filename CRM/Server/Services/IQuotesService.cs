using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IQuotesService
    {
        Task<QuoteDTO?> GetItemAsync(int id);
        Task<PagingResponse<QuoteDTO, decimal>?> GetSummaryAsync(QuoteFilter? args);
        Task<List<QuoteDTO>?> GetListAsync(QuoteFilter? args = null);
        Task<APIResponseMessage<QuoteDTO>> PostAsync(Quote item);
        Task<APIResponseMessage<QuoteDTO>> ChangeStateAsync(int id, QuoteStates state, bool updateDeal);
        Task<(byte[] Bytes, string FileName)?> GeneratePdfAsync(int id);
        Task<bool> DeleteAsync(int id);
    }
}
