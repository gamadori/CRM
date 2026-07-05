using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IPriceListService
    {
        Task<List<PriceListItemDTO>> GetByCompanyAsync(int idCompany);
        Task<PriceListItemDTO?> ResolveAsync(int idCompany, int idProduct);
        Task<APIResponseMessage<PriceListItemDTO>> UpsertAsync(PriceListItem item);
        Task<bool> DeleteAsync(int id);
    }
}
