using CRM.Client.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace CRM.Client.Pages.Attachments
{
    [Authorize]
    public partial class Details : ComponentBase 
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }
        
        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Parameter]
        public int? Id { get; set; }


        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public Action OnClickAddFile { get; set; }

        [Parameter]
        public bool ReadOnly { get; set; } = false;


        private Func<Task> OnClickOk = null;

        private Attachment _attachment = null;

        private string _message;

        private string _messageHeader;

        private int? _idFileSelected = null;

        private bool _notFound = false;

        protected override async Task OnInitializedAsync()
        {
            await LoadAttachment();
        }

        protected async Task LoadAttachment()
        {
            string path;
            try
            {
                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.AttachmentsPath;

                if (Id != null)
                {
                    path += $"/{Id}";

                    _attachment = await Http.GetFromJsonAsync<Attachment>(path);

                    _notFound = (_attachment == null);
                    
                }
                else
                    _attachment = new Attachment();


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected void EditAttachment()
        {
            if (OnClickEdit != null)
                OnClickEdit(Id);
            else
                NavigationManager.NavigateTo($"/Attachments/Edit/{Id}");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo("/Attachments/Index");
        }

        protected void AddFile()
        {

            if (OnClickAddFile != null)
                OnClickAddFile();
            else
                NavigationManager.NavigateTo($"/Attachments/Upload/{Id}");
        }
        protected bool AnySelected()
        {
            if (_attachment.Files != null)
            {
                bool state = _attachment.Files.Where(x => x.Selected).Any();
                return state;
            }
            else
                return false;
        }

        protected void PrepareDelete()
        {
           
            _message = $"Eliminare gli allegati selezionati?";
            _messageHeader = "Delete";
            OnClickOk = OnClickDeleteConfirmed;
            StateHasChanged();
            JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");
        }

        protected void PrepareDownload()
        {
            _message = $"Scaricare gli allegati selezionati?";
            _messageHeader = "Download";
            OnClickOk = DownloadFile;
            StateHasChanged();
            JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");
        }

        protected async Task OnClickDeleteConfirmed()
        {
            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgDelete");

            string path = ConstHelper.AttachmentsPath;

            var resp = await Http.DeleteAsync($"{path}/files/{_idFileSelected}");
             
            await LoadAttachment();
            StateHasChanged();
        }

        protected async Task OnDownloadConfirmed()
        {
            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgDelete");

            var response = await Http.GetAsync($"{ConstHelper.AttachmentsPath}/files/download/{_idFileSelected}");

            if (response.IsSuccessStatusCode)
            {
                

                var bytes = await response.Content.ReadAsByteArrayAsync();


                AttachmentResponse header = JsonSerializer.Deserialize<AttachmentResponse>(response.Headers
                        .GetValues(ConstHelper.FileHeader).First(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });


                await JSRuntime.InvokeVoidAsync(
                  "downloadFromByteArray",
                  new
                  {
                      ByteArray = bytes,
                      FileName = header.Name,
                      ContentType = header.ContentType
                  });
            }
        
        }
        private async Task DownloadFile()
        {

            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgDelete");

            var response = await Http.PostAsJsonAsync<Attachment>($"{ConstHelper.AttachmentsPath}/files/download", _attachment);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();


                AttachmentResponse header = JsonSerializer.Deserialize<AttachmentResponse>(response.Headers
                        .GetValues(ConstHelper.FileHeader).First(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });


                await JSRuntime.InvokeVoidAsync(
                  "downloadFromByteArray",
                  new
                  {
                      ByteArray = bytes,
                      FileName = header.Name,
                      ContentType = header.ContentType
                  });
            }
        }

        private void OnDelete(int? id)
        {
            _idFileSelected = id;
            var file = _attachment.Files.Where(x => x.Id == id).FirstOrDefault();

            if (file != null)
            {
                _message = $"Eliminare il file {file.Name}";

                _messageHeader = "Delete";
                OnClickOk = OnClickDeleteConfirmed;
                StateHasChanged();

                JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");
                
            }
        }

        private void OnDownload(int? id)
        {
            _idFileSelected = id;
            var file = _attachment.Files.Where(x => x.Id == id).FirstOrDefault();

            if (file != null)
            {
                _message = $"Scaricare il file {file.Name}?";

                _messageHeader = "Downlaod";
                OnClickOk = OnDownloadConfirmed;

                StateHasChanged();

                JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");
            }

        }
        private async Task OnRename((int? IdFile, string NewName) args)
        {
            try
            {
                if (args.IdFile == null)
                {
                    Console.WriteLine("ID file non valido");
                    return;
                }

                if (string.IsNullOrWhiteSpace(args.NewName))
                {
                    Console.WriteLine("Il nuovo nome non può essere vuoto");
                    return;
                }

                var nameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(args.NewName);
                
                var response = await Http.PutAsJsonAsync($"api/Attachments/files/rename/{args.IdFile}", nameWithoutExtension);

                if (response.IsSuccessStatusCode)
                {
                    // Ricarica l'attachment per aggiornare la lista dei file
                    await LoadAttachment();
                    StateHasChanged();
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Errore durante la ridenominazione: {response.StatusCode} - {errorMessage}");
                    
                    // Opzionale: mostra un messaggio all'utente
                    _message = $"Errore durante la ridenominazione del file: {errorMessage}";
                    _messageHeader = "Errore";
                    StateHasChanged();
                    await JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");
                }
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore imprevisto durante la ridenominazione: {ex.Message}");
                
                // Opzionale: mostra un messaggio all'utente
                _message = $"Errore imprevisto durante la ridenominazione del file: {ex.Message}";
                _messageHeader = "Errore";
                StateHasChanged();
                await JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");
            }
        }
    }
}
