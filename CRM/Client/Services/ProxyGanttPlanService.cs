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
    public class ProxyGanttPlanService : IGanttPlanService
    {
        private readonly HttpClient _http;
        private readonly string _path = ConstHelper.GanttPlansPath;
        private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        public ProxyGanttPlanService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<GanttPlanDTO>> GetListAsync(GanttPlanFilter? args = null)
        {
            try
            {
                var query = new List<string>();
                if (args?.State != null) query.Add($"State={args.State}");
                if (!string.IsNullOrWhiteSpace(args?.Search)) query.Add($"Search={Uri.EscapeDataString(args.Search)}");
                var url = query.Count > 0 ? $"{_path}?{string.Join("&", query)}" : _path;
                return await _http.GetFromJsonAsync<List<GanttPlanDTO>>(url) ?? new();
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<GanttPlanDTO?> GetItemAsync(int id)
        {
            try
            {
                return await _http.GetFromJsonAsync<GanttPlanDTO>($"{_path}/{id}");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<APIResponseMessage<GanttPlanDTO>> SaveAsync(GanttPlanDTO dto)
        {
            try
            {
                var resp = dto.Id > 0
                    ? await _http.PutAsJsonAsync($"{_path}/{dto.Id}", dto)
                    : await _http.PostAsJsonAsync(_path, dto);

                if (resp.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<APIResponseMessage<GanttPlanDTO>>(
                        await resp.Content.ReadAsStringAsync(), _json)
                        ?? new APIResponseMessage<GanttPlanDTO> { State = false, Message = "Risposta vuota" };

                return new APIResponseMessage<GanttPlanDTO>
                {
                    State = false,
                    Code = resp.StatusCode,
                    Message = await resp.Content.ReadAsStringAsync()
                };
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new APIResponseMessage<GanttPlanDTO> { State = false, Code = System.Net.HttpStatusCode.Unauthorized };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<GanttPlanDTO> { State = false, Message = ex.Message };
            }
        }

        public async Task<APIResponseMessage<GanttPlanDTO>> DeleteAsync(int id)
        {
            try
            {
                var resp = await _http.DeleteAsync($"{_path}/{id}");
                if (resp.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<APIResponseMessage<GanttPlanDTO>>(
                        await resp.Content.ReadAsStringAsync(), _json)
                        ?? new APIResponseMessage<GanttPlanDTO> { State = false, Message = "Risposta vuota" };

                return new APIResponseMessage<GanttPlanDTO>
                {
                    State = false,
                    Code = resp.StatusCode,
                    Message = await resp.Content.ReadAsStringAsync()
                };
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new APIResponseMessage<GanttPlanDTO> { State = false, Code = System.Net.HttpStatusCode.Unauthorized };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<GanttPlanDTO> { State = false, Message = ex.Message };
            }
        }
    }
}
