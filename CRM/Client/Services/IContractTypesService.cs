using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    

    public interface IContractTypesService : IBaseRestService<ContractType, ContractTypeFilter, int>
    {
    }
}
