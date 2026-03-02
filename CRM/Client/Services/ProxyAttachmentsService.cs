using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{

    public class ProxyAttachmentsService : ProxyRestClientService<Attachment, AttachmentDTO, int, AttachmentsFilter, object>, IAttachmentsService
    {
        public ProxyAttachmentsService(HttpClient http) : base(http, ConstHelper.AttachmentsPath)
        {

        }
        public async Task<bool> CanDelete(int id)
        { 
            return await _http.GetFromJsonAsync<bool>($"{_pathService}/CanDelete/{id}");
        }

        public async Task<bool> CanEdit(int id)
        {
            return await _http.GetFromJsonAsync<bool>($"{_pathService}/CanEdit/{id}");
        }

        public async Task<bool> UploadFiles(int idAttachment, List<AttachmentFile> files)
        {
            var resp = await _http.PostAsJsonAsync($"{_pathService}/upload/{idAttachment}", files);

            if (resp.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                return false;
            }   
            
        }

        public async Task<(byte[] Bytes, string ContentType, string FileName)> DownloadFiles(int id)
        {
            var response = await _http.GetAsync($"{_pathService}/download/{id}");

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();


                AttachmentResponse header = JsonSerializer.Deserialize<AttachmentResponse>(response.Headers
                        .GetValues(ConstHelper.FileHeader).First(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });


                return (bytes, header.ContentType, header.Name);
            }
            return (Array.Empty<byte>(), string.Empty, string.Empty);
        }
        public async Task<bool> DeleteFiles(int id)
        {
            var resp = await _http.DeleteAsync($"{_pathService}/files/{id}");

            if (resp.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }
}
