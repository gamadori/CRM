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
    public class ProxyActivityService : IActivityService
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        public ProxyActivityService(HttpClient http)
        {
            _http = http;
        }

        public async Task<ActivityDTO?> GetAsync(int id)
        {
            try
            {
                return await _http.GetFromJsonAsync<ActivityDTO>($"{ConstHelper.ActivitiesPath}/{id}");
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

        public async Task<List<ActivityDTO>> GetByEntityAsync(ActivityEntityType entityType, int entityId)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<ActivityDTO>>(
                    $"{ConstHelper.ActivitiesPath}/by-entity/{entityType}/{entityId}") ?? new List<ActivityDTO>();
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new List<ActivityDTO>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new List<ActivityDTO>();
            }
        }

        public async Task<List<ActivityDTO>> GetMyAgendaAsync(ActivityFilter? filter = null)
        {
            try
            {
                var qs = "";
                if (filter != null)
                {
                    var parts = new List<string>();
                    if (filter.State != null) parts.Add($"state={filter.State}");
                    if (filter.DateFrom != null) parts.Add($"dateFrom={filter.DateFrom:yyyy-MM-dd}");
                    if (filter.DateTo != null) parts.Add($"dateTo={filter.DateTo:yyyy-MM-dd}");
                    if (parts.Count > 0) qs = "?" + string.Join("&", parts);
                }
                return await _http.GetFromJsonAsync<List<ActivityDTO>>(
                    $"{ConstHelper.ActivitiesPath}/my-agenda{qs}") ?? new List<ActivityDTO>();
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new List<ActivityDTO>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new List<ActivityDTO>();
            }
        }

        public async Task<APIResponseMessage<ActivityDTO>> PostAsync(Activity item)
        {
            try
            {
                HttpResponseMessage resp;
                if (item.Id > 0)
                    resp = await _http.PutAsJsonAsync($"{ConstHelper.ActivitiesPath}/{item.Id}", item);
                else
                    resp = await _http.PostAsJsonAsync(ConstHelper.ActivitiesPath, item);

                return await ReadResponse(resp);
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new APIResponseMessage<ActivityDTO> { State = false, Code = System.Net.HttpStatusCode.Unauthorized };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<ActivityDTO> { State = false, Message = ex.Message };
            }
        }

        public async Task<APIResponseMessage<ActivityDTO>> CompleteAsync(int id)
        {
            try
            {
                var resp = await _http.PostAsync($"{ConstHelper.ActivitiesPath}/{id}/complete", null);
                return await ReadResponse(resp);
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<ActivityDTO> { State = false, Message = ex.Message };
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var resp = await _http.DeleteAsync($"{ConstHelper.ActivitiesPath}/{id}");
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        private static async Task<APIResponseMessage<ActivityDTO>> ReadResponse(HttpResponseMessage resp)
        {
            if (resp.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<APIResponseMessage<ActivityDTO>>(
                    await resp.Content.ReadAsStringAsync(), _json)
                    ?? new APIResponseMessage<ActivityDTO> { State = false, Message = "null" };
            }
            return new APIResponseMessage<ActivityDTO>
            {
                State = false,
                Code = resp.StatusCode,
                Message = $"{resp.ReasonPhrase}\n\r{await resp.Content.ReadAsStringAsync()}"
            };
        }
    }
}
