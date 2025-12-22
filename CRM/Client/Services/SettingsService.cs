using CRM.Client.Helpers;
using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class SettingsService<T>: ISettingsService<T> where T : class
    {
        private readonly HttpClient _httpClient;
        private readonly string _pathService;

        public SettingsService(HttpClient http, string path)
        {
            _httpClient = http;
            _pathService = path;
        }
        public async Task<T> Get()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<T>($"{_pathService}");

                return response;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<bool> Post(T item)
        {
            try
            {
                HttpResponseMessage resp;

                var id = UtilityHelper.GetPropertyValue<int>(item, "Id");

                if (id > 0)
                    resp = await _httpClient.PutAsJsonAsync<T>($"{_pathService}/{id}", item);
                else
                    resp = await _httpClient.PostAsJsonAsync<T>(_pathService, item);

                return resp.IsSuccessStatusCode;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
    }
}
