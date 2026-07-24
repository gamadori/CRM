using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyMachineBackupsService : IMachineBackupsService
    {
        private const string Path = "api/MachineBackups";
        private const int ChunkSize = 16 * 1024 * 1024;
        private const long MaximumChunkedFileSize = 5L * 1024 * 1024 * 1024;
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
            string? externalReference,
            IProgress<double>? progress = null)
        {
            if (file.Size > MaximumChunkedFileSize)
            {
                throw new InvalidOperationException("Il file supera il limite massimo di 5 GB.");
            }

            var uploadId = Guid.NewGuid().ToString("D");
            var totalChunks = Math.Max(1, (int)Math.Ceiling(file.Size / (double)ChunkSize));
            await using var source = file.OpenReadStream(MaximumChunkedFileSize);
            var buffer = new byte[ChunkSize];
            MachineBackupDTO? created = null;

            for (var chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
            {
                var bytesRead = await ReadChunkAsync(source, buffer);
                if (bytesRead == 0)
                {
                    break;
                }

                using var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(buffer, 0, bytesRead);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
                content.Add(fileContent, "chunk", $"{file.Name}.part{chunkIndex}");
                content.Add(new StringContent(uploadId), "uploadId");
                content.Add(new StringContent(chunkIndex.ToString()), "chunkIndex");
                content.Add(new StringContent(totalChunks.ToString()), "totalChunks");
                content.Add(new StringContent(file.Size.ToString()), "totalSize");
                content.Add(new StringContent(file.Name), "fileName");
                content.Add(new StringContent(file.ContentType ?? string.Empty), "contentType");
                content.Add(new StringContent(description ?? string.Empty), "description");
                content.Add(new StringContent(externalReference ?? string.Empty), "externalReference");

                var response = await _http.PostAsync($"{Path}/{ownerType}/{ownerId}/chunks", content);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<MachineBackupChunkUploadResult>();

                progress?.Report(Math.Min(100, ((chunkIndex + 1) / (double)totalChunks) * 100));
                if (result?.IsComplete == true)
                {
                    created = result.Backup;
                }
            }

            return created;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var response = await _http.DeleteAsync($"{Path}/{id}");
            return response.IsSuccessStatusCode;
        }

        private static async Task<int> ReadChunkAsync(Stream source, byte[] buffer)
        {
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = await source.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead));
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            return totalRead;
        }
    }
}
