using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.DTOs;
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
    
    public class ProxyDealService: ProxyRestClientService<Deal, DealDTO, int, DealFilter, decimal>, IDealService
    {
        
        public ProxyDealService(HttpClient http): base(http, ConstHelper.DealsPath)
        {
          
        }
        
       
    }
}
