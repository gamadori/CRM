using CRM.Shared;
using System.Threading.Tasks;
using Contact = CRM.Shared.Contact;

namespace CRM.Client.Services
{
    public interface IProjectsService: IAGRestClientService
    {
        Task<bool> AddUser(ProjectUser item);

        Task<bool> RemoveUser(ProjectUser item);

    }


}
