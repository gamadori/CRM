using CRM.Client.Helpers;
using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ReportService<T, F>: IReportService<T, F> where T : class
    {
        private readonly HttpClient _httpClient;
        private readonly string _pathService;

        public ReportService(HttpClient http, string path)
        {
            _httpClient = http;
            _pathService = path;
        }
        public async Task<T> Get(F filter)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();


                Type returnType = filter.GetType();
                var fields = returnType.GetProperties();


                foreach (var field in fields)
                {

                    string value;
                    var obj = UtilityHelper.GetPropertyValue<object>(filter, field.Name);

                    if (field.PropertyType == typeof(DateTime) || (field.PropertyType == typeof(DateTime?) && obj != null))
                        value = ((DateTime)obj).ToString("yyyy-MM-dd");
                    else
                        value = obj?.ToString();


                    param.Add(field.Name, value);
                }


                var qs = UriHelper.BuildQueryString(param);

                var response = await _httpClient.GetAsync(_pathService + qs);



                if (response.IsSuccessStatusCode)
                {


                    var content = await response.Content.ReadAsStringAsync();

                    var resp = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

                    return resp;

                    
                }
                else
                    return null;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

       


        public async Task<List<T>> GetItems()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<T>>(_pathService);
                return response;
            }

            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
        
    }
}
