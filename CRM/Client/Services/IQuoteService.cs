using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IQuoteService : IDataService<Quote, QuoteDTO, int, QuoteFilter, decimal>
    {
        Task<APIResponseMessage<QuoteDTO>> ChangeStateAsync(int id, QuoteStates state, bool updateDeal = true);
        Task<APIResponseMessage<QuoteDTO>> SendAsync(int id, QuoteSendRequest request);

        /// <summary>Crea la revisione successiva: la corrente diventa storia, la nuova nasce in bozza.</summary>
        Task<APIResponseMessage<QuoteDTO>> CreateRevisionAsync(int id);
    }
}
