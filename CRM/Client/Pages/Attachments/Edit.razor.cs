using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.Extensions.Localization;
using CRM.Shared.Extensions;

namespace CRM.Client.Pages.Attachments
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IBaseRestService<Attachment, AttachmentsFilter, int> _service { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        IRestService<ApplicationUser> UserService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public int IdParent { get; set; }

        [Parameter]
        public AttachmentTypes AttachmentType { get; set; }

        [Parameter]
        public Action OnClickClose { get; set; }

        [Parameter]
        public string FileUrl { get; set; }

        private Attachment _attachment = null;

        private string _errorMessage = null;

        private List<FileWithName> _filesWithNames = new();

        private bool _waitingUpload = false;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                var user = await UserService.Get();

                if (Id != null)
                {
                    _attachment = await _service.Get(Id.Value);
                    _attachment.Files.Clear();
                }
                else if (user != null)
                    _attachment = new Attachment() { IdParent = IdParent, AttchmentType = AttachmentType, IdUser = user.Id, Files = new List<AttachmentFile>() };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected void Annulla()
        {
            if (OnClickClose != null)
                OnClickClose();
            else
                NavigationManager.NavigateTo("/Attachments/Index");
        }

        protected void DeleteFile(int idFile)
        {
            var f = _attachment.Files.Where(x => x.Id == idFile).FirstOrDefault();
            if (f != null)
                _attachment.Files.Remove(f);

            StateHasChanged();
        }

        // ✅ FIX: Usa InputFileChangeEventArgs (nativo Blazor) invece di Radzen
        private async Task OnInputFileChange(InputFileChangeEventArgs e)
        {
            _errorMessage = null;

            try
            {
                const long maxFileSize = 20 * 1024 * 1024; // 20MB

                foreach (var file in e.GetMultipleFiles(50))
                {
                    try
                    {
                        if (file.Size > maxFileSize)
                        {
                            _errorMessage = $"File {file.Name} troppo grande (max 20MB)";
                            continue;
                        }

                        // ✅ CRITICO: Leggi i bytes IMMEDIATAMENTE
                        byte[] fileBytes;
                        using var stream = file.OpenReadStream(maxFileSize);
                        using var memoryStream = new MemoryStream();
                        await stream.CopyToAsync(memoryStream);
                        fileBytes = memoryStream.ToArray();

                        _filesWithNames.Add(new FileWithName
                        {
                            OriginalFileName = file.Name,
                            NewName = Path.GetFileNameWithoutExtension(file.Name),
                            Extension = Path.GetExtension(file.Name),
                            FileBytes = fileBytes,
                            ContentType = file.ContentType,
                            Size = file.Size
                        });
                    }
                    catch (Exception ex)
                    {
                        _errorMessage = $"Errore lettura file {file.Name}: {ex.Message}";
                        Console.WriteLine($"Errore lettura file: {ex}");
                    }
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore durante la selezione dei file: {ex.Message}";
                Console.WriteLine($"Errore OnInputFileChange: {ex}");
                StateHasChanged();
            }
        }

        private void RemoveFile(FileWithName file)
        {
            _filesWithNames.Remove(file);
            StateHasChanged();
        }

        private List<AttachmentFile> PrepareFilesForUpload()
        {
            List<AttachmentFile> items = new List<AttachmentFile>();
            try
            {
                if (_filesWithNames != null && _filesWithNames.Any())
                {
                    foreach (var fileWrapper in _filesWithNames)
                    {
                        if (fileWrapper.FileBytes != null)
                        {
                            var finalName = $"{fileWrapper.NewName}{fileWrapper.Extension}";

                            AttachmentFile f = new AttachmentFile()
                            {
                                Content = Convert.ToBase64String(fileWrapper.FileBytes),
                                ContentType = fileWrapper.ContentType,
                                Name = finalName,
                                Size = fileWrapper.Size
                            };

                            items.Add(f);
                        }
                    }
                }
                return items;
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore durante la preparazione dei file: {ex.Message}";
                Console.WriteLine(ex);
                return null;
            }
        }

        protected async Task HandleValidSubmit()
        {
            try
            {
                _waitingUpload = true;
                _errorMessage = null;

                var items = PrepareFilesForUpload();

                if (_attachment == null)
                    _attachment = new Attachment();

                if (_attachment.Files == null)
                    _attachment.Files = new List<AttachmentFile>();
                
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        _attachment.Files.Add(item);
                    }
                }
                
                var resp = await _service.Post(_attachment);

                if (resp != null)
                {
                    if (resp.State)
                    {
                        if (OnClickClose != null)
                            OnClickClose();
                        else
                            NavigationManager.NavigateTo("/Attachments");
                        _errorMessage = null;
                    }
                    else
                        _errorMessage = $"{resp.Message}";
                }
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore durante l'upload: {ex.Message}";
                Console.WriteLine(ex);
            }
            finally
            {
                _waitingUpload = false;
                StateHasChanged();
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public class FileWithName
        {
            public string OriginalFileName { get; set; }
            public string NewName { get; set; }
            public string Extension { get; set; }
            public byte[] FileBytes { get; set; }
            public string ContentType { get; set; }
            public long Size { get; set; }
        }
    }
}
