using CRM.Shared;
using System.Threading.Tasks;
using Contact = CRM.Shared.Contact;

namespace CRM.Client.Services
{
    public interface ICompaniesService: IAGRestClientService
    {
        Task<bool> AddCustomer(CustomerModel item);

        Task<bool> RemoveCustomer(CustomerModel item);

        Task<Company?> GetCompany();
    }


}
