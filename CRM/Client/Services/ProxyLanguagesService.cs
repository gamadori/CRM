using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.Models;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyLanguagesService: ProxyRestClientService<Language, int, LanguageFilter, object>, ILanguagesService
    {

        public ProxyLanguagesService(HttpClient http) : base(http, ConstHelper.LanguagesPath)
        {

        }

        public async Task<int?> GetIdLanguage()
        {
            try
            {
                var id = await _http.GetFromJsonAsync<int>($"{_pathService}/GetIdLanguage");

                return id;
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
        public async Task<bool> SetIdLanguage(int id)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync<int>($"{_pathService}/SetIdLanguage", id);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public async Task<string?> GetCodeLanguage()
        {
            try
            {
                var code = await _http.GetStringAsync($"{_pathService}/GetCodeLanguage");

                return code;
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<bool> SetCodeLanguage(string code)
        { 
            try
            {
                var resp = await _http.PostAsJsonAsync<string>($"{_pathService}/SetCodeLanguage", code);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

    }
}
