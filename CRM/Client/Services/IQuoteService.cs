using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IQuoteService : IDataService<Quote, QuoteDTO, int, QuoteFilter, decimal>
    {
        Task<APIResponseMessage<QuoteDTO>> ChangeStateAsync(int id, QuoteStates state, bool updateDeal = true);
    }
}
