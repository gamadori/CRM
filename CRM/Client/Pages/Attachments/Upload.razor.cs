using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace CRM.Client.Pages.Attachments
{
    [Authorize]
    public partial class Upload : ComponentBase
    {
        [Inject]
        private HttpClient _http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IBaseRestService<Attachment, AttachmentsFilter, int> _service { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnClickClose { get; set; }

        [Parameter]
        public string FileUrl { get; set; }

        private Attachment _attachment = null;

        private string _fileUrl = string.Empty;

        private string _errorMessage = null;

        //Variabile per la Progress Bar
        private bool _waitingUpload = false;

        private List<FileWithName> _filesWithNames = new();

        protected override async Task OnInitializedAsync()
        {
            try
            {
                _attachment = await _service.Get(Id);
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
                NavigationManager.NavigateTo($"/Attachments/{Id}");
        }

        void OnChange(UploadChangeEventArgs args)
        {
            foreach (var file in args.Files)
            {
                _filesWithNames.Add(new FileWithName
                {
                    FileInfo = file,
                    NewName = Path.GetFileNameWithoutExtension(file.Name),
                    Extension = Path.GetExtension(file.Name)
                });
            }
            StateHasChanged();
        }

        private void RemoveFile(FileWithName file)
        {
            _filesWithNames.Remove(file);
            StateHasChanged();
        }

        protected async Task HandleValidSubmit()
        {
            _errorMessage = null;
            _waitingUpload = true;
            StateHasChanged();

            try
            {
                if (_filesWithNames == null || !_filesWithNames.Any())
                {
                    _errorMessage = "Nessun file selezionato per l'upload";
                    _waitingUpload = false;
                    StateHasChanged();
                    return;
                }

                var filesToUpload = await LoadFilesAsync();
                
                if (filesToUpload == null || !filesToUpload.Any())
                {
                    _waitingUpload = false;
                    StateHasChanged();
                    return;
                }

                var resp = await _http.PostAsJsonAsync($"{ConstHelper.AttachmentsPath}/upload/{Id}", filesToUpload);

                if (resp != null && resp.IsSuccessStatusCode)
                {
                    if (OnClickClose != null)
                        OnClickClose();
                    else
                        NavigationManager.NavigateTo($"/Attachments/{Id}");
                }
                else
                {
                    var errorContent = await resp.Content.ReadAsStringAsync();
                    _errorMessage = $"Errore nel Server: {resp.StatusCode} - {errorContent}";
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

        private async Task<List<AttachmentFile>> LoadFilesAsync()
        {
            List<AttachmentFile> items = new List<AttachmentFile>();
            
            try
            {
                if (_filesWithNames != null && _filesWithNames.Any())
                {
                    foreach (var fileWrapper in _filesWithNames)
                    {
                        var file = fileWrapper.FileInfo;
                        if (file != null)
                        {
                            var stream = file.OpenReadStream(ConstHelper.MaxAllowSize);
                            byte[] buf = await stream.CopyToArrayAsync();
                            stream.Close();

                            var finalName = $"{fileWrapper.NewName}{fileWrapper.Extension}";

                            AttachmentFile f = new AttachmentFile()
                            {
                                Content = Convert.ToBase64String(buf),
                                Name = finalName,
                                Size = file.Size
                            };

                            items.Add(f);
                        }
                    }
                }
                return items;
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore durante la lettura dei file: {ex.Message}";
                Console.WriteLine(ex);
                return null;
            }
        }

        public class FileWithName
        {
            public Radzen.FileInfo FileInfo { get; set; }
            public string NewName { get; set; }
            public string Extension { get; set; }
        }
    }
}
