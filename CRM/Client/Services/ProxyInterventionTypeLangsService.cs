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

    public class ProxyInterventionTypeLangsService : ProxyRestClientService<InterventionTypeLanguage, InterventionTypeLangDTO, int, InterventionTypeLangFilter, object>, IInterventionTypeLangsService
    {
        public ProxyInterventionTypeLangsService(HttpClient http) : base(http, ConstHelper.InterventionTypeLangsPath)
        {

        }
        public async Task<string?> GetFlagAsync(int id)
        {
            try
            {
                var flag = await _http.GetFromJsonAsync<string>($"{_pathService}/Flag/{id}");
                return flag;
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
