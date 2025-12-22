using CRM.Client.Helpers;
using CRM.Shared;
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
    
    public class TicketTypesService: RestClientService<TicketType, TicketTypeFilter, int>, ITicketTypesService
    {
        public TicketTypesService(HttpClient http): base(http, ConstHelper.TicketTypesPath)
        {
          
        }
    }
}
