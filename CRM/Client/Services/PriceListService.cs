using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class PriceListService : IPriceListService
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        public PriceListService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<PriceListItemDTO>> GetByCompanyAsync(int idCompany)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<PriceListItemDTO>>($"{ConstHelper.PriceListPath}/by-company/{idCompany}")
                       ?? new List<PriceListItemDTO>();
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new List<PriceListItemDTO>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new List<PriceListItemDTO>();
            }
        }

        public async Task<PriceListItemDTO?> ResolveAsync(int idCompany, int idProduct)
        {
            try
            {
                return await _http.GetFromJsonAsync<PriceListItemDTO?>(
                    $"{ConstHelper.PriceListPath}/resolve?idCompany={idCompany}&idProduct={idProduct}");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<APIResponseMessage<PriceListItemDTO>> UpsertAsync(PriceListItem item)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync(ConstHelper.PriceListPath, item);
                if (resp.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<APIResponseMessage<PriceListItemDTO>>(
                        await resp.Content.ReadAsStringAsync(), _json)
                        ?? new APIResponseMessage<PriceListItemDTO> { State = false, Message = "null" };
                }
                return new APIResponseMessage<PriceListItemDTO>
                {
                    State = false,
                    Code = resp.StatusCode,
                    Message = $"{resp.ReasonPhrase}\n\r{await resp.Content.ReadAsStringAsync()}"
                };
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new APIResponseMessage<PriceListItemDTO> { State = false, Code = System.Net.HttpStatusCode.Unauthorized };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new APIResponseMessage<PriceListItemDTO> { State = false, Message = ex.Message };
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var resp = await _http.DeleteAsync($"{ConstHelper.PriceListPath}/{id}");
                return resp.IsSuccessStatusCode;
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
    }
}
