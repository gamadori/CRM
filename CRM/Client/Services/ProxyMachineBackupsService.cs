using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyMachineBackupsService : IMachineBackupsService
    {
        private const string Path = "api/MachineBackups";
        private const long MaximumFileSize = 512L * 1024 * 1024;
        private readonly HttpClient _http;

        public ProxyMachineBackupsService(HttpClient http)
        {
            _http = http;
        }

        public async Task<MachineBackupListDTO> GetListAsync(MachineBackupOwnerType ownerType, int ownerId, int skip = 0, int take = 50)
        {
            var url = $"{Path}?ownerType={(int)ownerType}&ownerId={ownerId}&skip={skip}&take={take}";
            return await _http.GetFromJsonAsync<MachineBackupListDTO>(url) ?? new MachineBackupListDTO();
        }

        public async Task<MachineBackupDTO?> UploadAsync(
            MachineBackupOwnerType ownerType,
            int ownerId,
            IBrowserFile file,
            string? description,
            string? externalReference)
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(file.OpenReadStream(MaximumFileSize));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
            content.Add(fileContent, "file", file.Name);
            content.Add(new StringContent(description ?? string.Empty), "description");
            content.Add(new StringContent(externalReference ?? string.Empty), "externalReference");

            var response = await _http.PostAsync($"{Path}/{ownerType}/{ownerId}", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MachineBackupDTO>();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var response = await _http.DeleteAsync($"{Path}/{id}");
            return response.IsSuccessStatusCode;
        }
    }
}
