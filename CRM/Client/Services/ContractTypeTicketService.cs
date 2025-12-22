using CRM.Client.Helpers;
using CRM.Shared;
using System.Net.Http;

namespace CRM.Client.Services
{
    public class ContractTypeTicketService : RestClientModelService<ContractTypeTicketType, ContractTypeTicketTypeModel, ContractTypeTicketTypeFilter, int>, IContractTypeTicketService
    {

        public ContractTypeTicketService(HttpClient http) : base(http, ConstHelper.ContractTypeTicketTypesPath)
        {

        }
    }
}
