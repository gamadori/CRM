using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IOrderService : IDataService<Order, OrderDTO, int, OrderFilter, decimal>
    {
        Task<APIResponseMessage<OrderDTO>> CreateFromQuoteAsync(int quoteId);
        Task<APIResponseMessage<OrderDTO>> ChangeStateAsync(int id, OrderStates state);
    }
}
