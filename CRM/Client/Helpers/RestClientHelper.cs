using CRM.Shared;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Helpers
{
    public static class RestClientHelper
    {
        public static async Task<HttpResponseMessage?> Get(HttpClient http, string pathService, PagingParameterModel data, Dictionary<string, string>? parameters = null)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();


                Type returnType = data.GetType();
                var fields = returnType.GetProperties();


                foreach (var field in fields)
                {

                    string? value;
                    var obj = UtilityHelper.GetPropertyValue<object>(data, field.Name);

                    if (obj != null && (field.PropertyType == typeof(DateTime) || (field.PropertyType == typeof(DateTime?))))
                    {
                        if (((DateTime)obj) > DateTime.MinValue)
                            value = ((DateTime)obj).ToString("yyyy-MM-dd");
                        else
                            value = null;
                    }
                    else
                        value = obj?.ToString();


                    if (value != null)
                        param.Add(field.Name, value);
                }

                if (parameters != null)
                {
                    foreach (var p in parameters)
                    {
                        param.Add(p.Key, p.Value);
                    }
                }
                var qs = UriHelper.BuildQueryString(param);

                var response = await http.GetAsync(pathService + qs);


                return response;

            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return null;
            }
            //catch (HttpRequestException ex)
            //{
            //    return null;

            //}

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
        public static async Task<PagingResponse<T>> Get<T>(HttpClient http, string pathService, PagingParameterModel data, Dictionary<string, string>? parameters = null) where T : class, new()
        {
            try
            {


                var response = await Get(http, pathService, data, parameters);


                if (response.IsSuccessStatusCode)
                {


                    var content = await response.Content.ReadAsStringAsync();

                    var pagingResponse = new PagingResponse<T>()
                    {
                        Items = JsonSerializer.Deserialize<List<T>>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }),
                        MetaData = JsonSerializer.Deserialize<PagingHeaderModel>(response.Headers
                            .GetValues(ConstHelper.PagingHeader).First(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true })
                    };
                    return pagingResponse;
                }
                else
                    return null;
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return null;
            }
            //catch (HttpRequestException ex)
            //{
            //    return null;

            //}

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

       
    }
}
