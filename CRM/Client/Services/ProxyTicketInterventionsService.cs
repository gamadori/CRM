using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.Models;
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
    
    public class ProxyTicketInterventionsService: RestClientService<TicketIntervention, TicketInterventionFilter, int>, ITicketInterventionsService
    {
        public ProxyTicketInterventionsService(HttpClient http) : base(http, ConstHelper.TicketsInterventionsPath)
        {

        }

        public async Task<bool> UploadReport(int id, UploadFilesModel item)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{_pathService}/UploadReport/{id}", item);
                if (resp.IsSuccessStatusCode)
                {
                    return await resp.Content.ReadFromJsonAsync<bool>();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore upload report: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateReport(int id, string? languageCode = null)
        {
            try
            {
                var url = $"{_pathService}/Report/{id}";
                if (!string.IsNullOrEmpty(languageCode))
                    url += $"?languageCode={languageCode}";

                var result = await _http.GetFromJsonAsync<bool>(url);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore creazione report: {ex.Message}");
                return false;
            }
        }

        public async Task<string?> GetReport(int id)
        {
            try
            {
                return await _http.GetFromJsonAsync<string?>($"{_pathService}/getreport/{id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore get report: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SendReportEmail(int id, EmailViewModel email)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{_pathService}/Email/{id}", email);
                if (resp.IsSuccessStatusCode)
                {
                    return await resp.Content.ReadFromJsonAsync<bool>();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore invio email report: {ex.Message}");
                return false;
            }
        }

        public async Task<List<string>> GetCompanyEmailAddresses(int id)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<string>>($"{_pathService}/CompanyEmailAdresses/{id}")
                    ?? new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento email azienda: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<HttpResponseMessage> AssignUsers(int id, List<string> userIds)
        {
            try
            {
                return await _http.PostAsJsonAsync($"{_pathService}/{id}/assign-users", userIds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore assegnazione utenti intervento: {ex.Message}");
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<HttpResponseMessage> SaveSignature(int id, SignatureData signatureData)
        {
            try
            {
                return await _http.PostAsJsonAsync($"{_pathService}/SaveSignature/{id}", signatureData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore salvataggio firma: {ex.Message}");
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<HttpResponseMessage> SaveSignatureWithEmailConfirmation(int id, SignatureDataWithEmail signatureData)
        {
            try
            {
                return await _http.PostAsJsonAsync($"{_pathService}/SaveSignatureWithEmailConfirmation/{id}", signatureData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore salvataggio firma con email: {ex.Message}");
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<HttpResponseMessage> ResendSignatureConfirmation(int id, ResendEmailRequest request)
        {
            try
            {
                return await _http.PostAsJsonAsync($"{_pathService}/ResendSignatureConfirmation/{id}", request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore rinvio email conferma: {ex.Message}");
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
            }
        }
    }
}
