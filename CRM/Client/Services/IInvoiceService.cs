using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IInvoiceService : IDataService<Invoice, InvoiceDTO, int, InvoiceFilter, decimal>
    {
        Task<APIResponseMessage<InvoiceDTO>> CreateFromOrderAsync(int orderId);
        Task<APIResponseMessage<InvoiceDTO>> SendAsync(int id);
        Task<APIResponseMessage<InvoiceDTO>> UpdateRecipientAsync(int id, string? codiceDestinatario);
    }
}
