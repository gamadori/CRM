using CRM.Shared;
using Contact = CRM.Shared.Contact;

namespace CRM.Client.Services
{
    public interface IContractTypeTicketService : IRestClientModelService<ContractTypeTicketType, ContractTypeTicketTypeModel, ContractTypeTicketTypeFilter, int>
    {
    }
}
