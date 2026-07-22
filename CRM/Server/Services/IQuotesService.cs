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

        /// <summary>Congela la revisione corrente e ne crea una nuova in bozza, stesso numero.</summary>
        Task<APIResponseMessage<QuoteDTO>> CreateRevisionAsync(int id);

        /// <summary>Tutte le revisioni del preventivo, dalla più recente.</summary>
        Task<List<QuoteRevisionDTO>> GetRevisionsAsync(int id);
        Task<(byte[] Bytes, string FileName)?> GeneratePdfAsync(int id);
        Task<(byte[] Bytes, string FileName)?> GetPdfAsync(int id);
        Task<APIResponseMessage<QuoteDTO>> SendAsync(int id, QuoteSendRequest request);
        Task<bool> DeleteAsync(int id);
    }
}
