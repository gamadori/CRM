using CRM.Client.Helpers;
using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ManyToManyService<T> : IManyToManyService<T> 
    {
        private readonly HttpClient _http;

        private readonly string _pathService;

        
        public ManyToManyService(HttpClient http, string path)
        {
            _http = http;
            _pathService = path;
        }

        public async Task<bool> Post(T item)
        {
            
            try
            {
                HttpResponseMessage resp;

               
                resp = await _http.PostAsJsonAsync<T>($"{_pathService}", item);
                
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public async Task<bool> Delete(T item)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();

                Type returnType = item.GetType();
                var fields = returnType.GetProperties();




                foreach (var field in fields)
                {
                    
                    param.Add(field.Name, UtilityHelper.GetPropertyValue<object>(item, field.Name)?.ToString());
                }

               

                var qs = UriHelper.BuildQueryString(param);



                var resp = await _http.DeleteAsync(_pathService + qs);

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
