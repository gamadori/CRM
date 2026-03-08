using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;
using Contact = CRM.Shared.Contact;

namespace CRM.Client.Services
{
    public interface ICompaniesService : IDataService<Company, CompanyDTO, int, CompanyFilter, object>
    {
        Task<bool> AddCustomer(CustomerModel item);
        
        Task<bool> RemoveCustomer(CustomerModel item);
        
        Task<IEnumerable<string>> GetEmailAddress(int idCompany);

        Task<CompanyDTO?> GetUserCompany();

        Task<string?> GetLogo(int idCompany);

        Task<List<CompanyTreeNodeDTO>> GetTreeAsync(int? idCompany = null);
    }
}
