using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IPriceListService
    {
        Task<List<PriceListItemDTO>?> GetByCompanyAsync(int idCompany);
        Task<PriceListItemDTO?> ResolveAsync(int idCompany, int idProduct);
        Task<APIResponseMessage<PriceListItemDTO>> UpsertAsync(PriceListItem item);
        Task<bool> DeleteAsync(int id);
    }
}
