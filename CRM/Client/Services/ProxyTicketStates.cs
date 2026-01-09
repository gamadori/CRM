using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    
    public class ProxyTicketsStates: ProxyRestClientService<TicketState, int, TicketStateFilter, object>, ITicketStatesService
    {
        public ProxyTicketsStates(HttpClient http) : base(http, ConstHelper.TicketStatesPath)
        {

        }

    }
}
