using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.DTOs;
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

    public class ProxyInterventionTypesService : ProxyRestClientService<InterventionType, InterventionTypeDTO, int, InterventionTypeFilter, object>, IInterventionTypesService
    {
        public ProxyInterventionTypesService(HttpClient http) : base(http, ConstHelper.InterventionTypesPath)
        {

        }
        public async Task<string> Translate(int id)
        {
            try
            {
                var response = await _http.GetFromJsonAsync<string>($"{_pathService}/{id}/translate");
                return response ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore traduzione tipo intervento: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
