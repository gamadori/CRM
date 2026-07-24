using CRM.Client.Helpers;
using CRM.Client.Models;
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
    public class ProxyGanttPhaseService : IGanttPhaseService
    {
        private readonly HttpClient _http;
        private readonly string _path = ConstHelper.GanttPhasesPath;
        private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        public ProxyGanttPhaseService(HttpClient http) => _http = http;

        public async Task<List<GanttPhaseDTO>> GetTreeAsync(int idGanttPlan)
        {
            try { return await _http.GetFromJsonAsync<List<GanttPhaseDTO>>($"{_path}/plan/{idGanttPlan}") ?? new(); }
            catch (AccessTokenNotAvailableException ex) { ex.Redirect(); return new(); }
            catch (Exception ex) { Console.WriteLine(ex.Message); return new(); }
        }

        public async Task<APIResponseMessage<GanttPhaseDTO>> SaveAsync(GanttPhaseDTO dto)
        {
            try
            {
                var resp = dto.Id > 0
                    ? await _http.PutAsJsonAsync($"{_path}/{dto.Id}", dto)
                    : await _http.PostAsJsonAsync(_path, dto);
                if (resp.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<APIResponseMessage<GanttPhaseDTO>>(await resp.Content.ReadAsStringAsync(), _json)
                        ?? new APIResponseMessage<GanttPhaseDTO> { State = false, Message = "null" };
                return new APIResponseMessage<GanttPhaseDTO> { State = false, Code = resp.StatusCode, Message = await resp.Content.ReadAsStringAsync() };
            }
            catch (Exception ex) { return new APIResponseMessage<GanttPhaseDTO> { State = false, Message = ex.Message }; }
        }

        public async Task<bool> DeleteAsync(int phaseId)
        {
            try { return (await _http.DeleteAsync($"{_path}/{phaseId}")).IsSuccessStatusCode; }
            catch { return false; }
        }

        public async Task<APIResponseMessage<GanttPhaseDependencyDTO>> AddDependencyAsync(GanttPhaseDependencyDTO dto)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{_path}/dependency", dto);
                if (resp.IsSuccessStatusCode)
                    return JsonSerializer.Deserialize<APIResponseMessage<GanttPhaseDependencyDTO>>(await resp.Content.ReadAsStringAsync(), _json)
                        ?? new APIResponseMessage<GanttPhaseDependencyDTO> { State = false, Message = "null" };
                return new APIResponseMessage<GanttPhaseDependencyDTO> { State = false, Code = resp.StatusCode, Message = await resp.Content.ReadAsStringAsync() };
            }
            catch (Exception ex) { return new APIResponseMessage<GanttPhaseDependencyDTO> { State = false, Message = ex.Message }; }
        }

        public async Task<bool> RemoveDependencyAsync(int dependencyId)
        {
            try { return (await _http.DeleteAsync($"{_path}/dependency/{dependencyId}")).IsSuccessStatusCode; }
            catch { return false; }
        }
    }
}
