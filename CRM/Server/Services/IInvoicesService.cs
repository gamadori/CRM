using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IInvoicesService
    {
        Task<InvoiceDTO?> GetItemAsync(int id);
        Task<PagingResponse<InvoiceDTO, decimal>?> GetSummaryAsync(InvoiceFilter? args);
        Task<List<InvoiceDTO>?> GetListAsync(InvoiceFilter? args = null);
        Task<APIResponseMessage<InvoiceDTO>> PostAsync(Invoice item);
        Task<APIResponseMessage<InvoiceDTO>> CreateFromOrderAsync(int orderId);
        Task<APIResponseMessage<InvoiceDTO>> UpdateRecipientAsync(int id, string? codiceDestinatario);
        Task<(string Xml, string FileName)?> GenerateXmlAsync(int id);
        Task<APIResponseMessage<InvoiceDTO>> SendAsync(int id);
        Task<bool> DeleteAsync(int id);
    }
}
