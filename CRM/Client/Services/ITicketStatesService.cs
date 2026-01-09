using CRM.Shared;
using CRM.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ITicketStatesService : IDataService<TicketState, int, TicketStateFilter, object>
    {
    }
}
