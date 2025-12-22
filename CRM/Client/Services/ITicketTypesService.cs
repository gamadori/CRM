using CRM.Shared;

namespace CRM.Client.Services
{
    public interface ITicketTypesService : IBaseRestService<TicketType, TicketTypeFilter, int>
    {
    }
}
