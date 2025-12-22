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

    public class CompanyContractsService : RestClientService<CompanyContract, CompanyContractFilter, int>, ICompanyContractsService
    {

        public CompanyContractsService(HttpClient http) : base(http, ConstHelper.CompanyContractsPath)
        {

        }

        public async Task<List<CompanyContract>?> CheckContractActive(CompanyContract item)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync<CompanyContract>($"{_pathService}/check", item);

                if (resp.IsSuccessStatusCode)
                {
                    var content = await resp.Content.ReadAsStringAsync();

                    var items = JsonSerializer.Deserialize<List<CompanyContract>>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                    return items;
                }
                else
                    return null;
            }
            catch
            {
                return null;
            }
        }

    }
}
