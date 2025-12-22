using CRM.Shared;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class IdentityRestClientService : RestClientService<Company, CompanyFilter, int>, IIdentityRestService
    {
        public IdentityRestClientService(HttpClient http, string path) : base(http, path)
        {

        }

        public async Task<bool> Confirm(string id)
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<IdentityResult>($"{_pathService}/Confirm/{id}");

                return resp.Succeeded;
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
    }
}
