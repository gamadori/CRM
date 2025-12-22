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
    public class LogosService: ILogosService
    {
        private readonly HttpClient _http;

        public LogosService(HttpClient http)
        {
            _http = http;
        }

        public async Task<PagingResponse<Logo>> GetLogos(LogosFilterModel filter)
        {
            try
            {
                PagingResponse<Logo> pageLogos = new PagingResponse<Logo>();

                Dictionary<string, string> param = new Dictionary<string, string>();

                param.Add(nameof(filter.Codice), filter.Codice);
                param.Add(nameof(filter.Descrizione), filter.Descrizione);

                var qs = UriHelper.BuildQueryString(param);

                var response = await _http.GetAsync(ConstHelper.LogosPath + qs);

                if (response.IsSuccessStatusCode)
                {

                    var content = await response.Content.ReadAsStringAsync();

                    var pagingResponse = new PagingResponse<Logo>()
                    {
                        Items = JsonSerializer.Deserialize<List<Logo>>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }),
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
            catch (HttpRequestException ex)
            {
                Console.WriteLine(ex.Message);
                return null;

            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, ex);
                return null;
            }
        }

        public async Task<bool> PostLogo(Logo item)
        {
            try
            {
                HttpResponseMessage resp;

                if (item.Id > 0)
                    resp = await _http.PutAsJsonAsync<Logo>($"{ConstHelper.LogosPath}/{item.Id}", item);
                else
                    resp = await _http.PostAsJsonAsync<Logo>(ConstHelper.LogosPath, item);

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
