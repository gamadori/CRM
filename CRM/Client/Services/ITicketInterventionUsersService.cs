using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ITicketInterventionUsersService : IDataService<TicketInterventionUser, int, TicketInterventionUserFilter, object>
    {
        Task<HashSet<string>?> LoadAssignedUsers(int IdIntervention);

        Task<HttpResponseMessage> AssignUsersToIntervention(int IdIntervention, AssignUsersRequest Users);
    }
}
