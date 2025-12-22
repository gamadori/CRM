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
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.Extensions.Localization;
using CRM.Shared.Extensions;
using Radzen;

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

        private string _fileUrl = string.Empty;

        private string _errorMessage = null;

        private List<FileWithName> _filesWithNames = new();

        //Variabile per la Progress Bar
        private bool _waitingUpload = false;

        protected override async Task OnInitializedAsync()
        {

            try
            {
                //await Task.Delay(10000);      // changes are flushed again   

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
                                ContentType = file.ContentType,
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

        protected async Task HandleValidSubmit()
        {

            try
            {
                _waitingUpload = true;
                _errorMessage = null;

                var items = await LoadFilesAsync();

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

        public class FileWithName
        {
            public Radzen.FileInfo FileInfo { get; set; }
            public string NewName { get; set; }
            public string Extension { get; set; }
        }
    }
}
