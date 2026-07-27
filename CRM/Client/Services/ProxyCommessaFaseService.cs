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
    public class ProxyCommessaFaseService : ICommessaFaseService
    {
        private readonly HttpClient _http;
        private readonly string _path = ConstHelper.CommessaFasiPath;
        private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        public ProxyCommessaFaseService(HttpClient http)
        {
            _http = http;
        }

        public async Task<CommessaFaseDTO?> GetItemAsync(int faseId)
        {
            try
            {
                return await _http.GetFromJsonAsync<CommessaFaseDTO>($"{_path}/{faseId}");
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

        public async Task<List<CommessaFaseDTO>> GetTreeAsync(int idCommessa)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<CommessaFaseDTO>>($"{_path}/commessa/{idCommessa}") ?? new();
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new();
            }
        }

        public async Task<APIResponseMessage<CommessaFaseDTO>> SaveAsync(CommessaFaseDTO dto)
        {
            try
            {
                var resp = dto.Id > 0
                    ? await _http.PutAsJsonAsync($"{_path}/{dto.Id}", dto)
                    : await _http.PostAsJsonAsync(_path, dto);

                if (resp.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<APIResponseMessage<CommessaFaseDTO>>(
                        await resp.Content.ReadAsStringAsync(), _json)
                        ?? new APIResponseMessage<CommessaFaseDTO> { State = false, Message = "null" };
                }
                return new APIResponseMessage<CommessaFaseDTO>
                {
                    State = false,
                    Code = resp.StatusCode,
                    Message = await resp.Content.ReadAsStringAsync()
                };
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new APIResponseMessage<CommessaFaseDTO> { State = false, Code = System.Net.HttpStatusCode.Unauthorized };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<CommessaFaseDTO> { State = false, Message = ex.Message };
            }
        }

        public async Task<bool> BulkSaveAsync(List<CommessaFaseDTO> dtos)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{_path}/bulk", dtos);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> DeleteAsync(int faseId)
        {
            try
            {
                var resp = await _http.DeleteAsync($"{_path}/{faseId}");
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<APIResponseMessage<CommessaFaseDependencyDTO>> AddDependencyAsync(CommessaFaseDependencyDTO dto)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{_path}/dependency", dto);
                if (resp.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<APIResponseMessage<CommessaFaseDependencyDTO>>(
                        await resp.Content.ReadAsStringAsync(), _json)
                        ?? new APIResponseMessage<CommessaFaseDependencyDTO> { State = false, Message = "null" };
                }
                return new APIResponseMessage<CommessaFaseDependencyDTO>
                {
                    State = false,
                    Code = resp.StatusCode,
                    Message = await resp.Content.ReadAsStringAsync()
                };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<CommessaFaseDependencyDTO> { State = false, Message = ex.Message };
            }
        }

        public async Task<bool> RemoveDependencyAsync(int dependencyId)
        {
            try
            {
                var resp = await _http.DeleteAsync($"{_path}/dependency/{dependencyId}");
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<APIResponseMessage<CommessaFaseTicketPlanDTO>> GenerateTicketFromPlanAsync(int ticketPlanId)
        {
            try
            {
                var resp = await _http.PostAsync($"{_path}/ticket-plans/{ticketPlanId}/generate-ticket", null);
                if (resp.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<APIResponseMessage<CommessaFaseTicketPlanDTO>>(
                        await resp.Content.ReadAsStringAsync(), _json)
                        ?? new APIResponseMessage<CommessaFaseTicketPlanDTO> { State = false, Message = "null" };
                }

                return new APIResponseMessage<CommessaFaseTicketPlanDTO>
                {
                    State = false,
                    Code = resp.StatusCode,
                    Message = await resp.Content.ReadAsStringAsync()
                };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<CommessaFaseTicketPlanDTO> { State = false, Message = ex.Message };
            }
        }
    }
}
