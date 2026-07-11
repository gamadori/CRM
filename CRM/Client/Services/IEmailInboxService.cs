using System.Collections.Generic;
using System.Threading.Tasks;
using CRM.Shared;

namespace CRM.Client.Services
{
    public interface IEmailInboxService : IDataService<EmailInbox>
    {
        Task<List<EmailInbox>> GetListAsync();

        Task<EmailInbox?> GetItemAsync(int id);

        Task<bool> DeleteAsync(int id);
    }
}
