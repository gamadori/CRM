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
        private readonly HttpClient _httpClient;
        
        public ProxyDealService(HttpClient http): base(http, ConstHelper.DealsPath)
        {
            _httpClient = http;
        }

        public async Task<CommercialForecastDTO?> GetForecastAsync(DealForecastFilter? filter)
        {
            var query = filter == null
                ? string.Empty
                : $"?DateFrom={filter.DateFrom:yyyy-MM-dd}&DateTo={filter.DateTo:yyyy-MM-dd}&IdUser={Uri.EscapeDataString(filter.IdUser ?? string.Empty)}";

            return await _httpClient.GetFromJsonAsync<CommercialForecastDTO?>($"{ConstHelper.DealsPath}/forecast{query}");
        }
    }
}
