using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.TicketInterventions
{
    [Authorize]
    public partial class UploadReport : ComponentBase
    {
        [Inject]
        private ITicketInterventionsService Service { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        private DialogService DialogService { get; set; }

        [Inject]
        private NotificationService NotificationService { get; set; }

        [Inject]
        private IHeaderService HeaderService { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action<bool> OnUploadComplete { get; set; }

        [Parameter]
        public Action OnCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private TicketIntervention _intervention = null;
        private SelectedFileInfo _selectedFile = null;
        private string _errorMessage = string.Empty;
        private bool _uploading = false;
        private bool _uploadSuccess = false;
        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                _intervention = await Service.Get(Id);
                _pageHeader = await HeaderService.Create(PageMode);
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore caricamento intervento: {ex.Message}";
                Console.WriteLine(ex);
            }
        }

        private async Task OnInputFileChange(InputFileChangeEventArgs e)
        {
            _errorMessage = string.Empty;
            _uploadSuccess = false;

            try
            {
                var file = e.File;

                // Validazioni
                if (file == null)
                {
                    _errorMessage = "Nessun file selezionato";
                    return;
                }

                // Verifica tipo file
                if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) 
                    && !file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    _errorMessage = "Solo file PDF sono consentiti";
                    return;
                }

                // Verifica dimensione (max 20MB)
                const long maxFileSize = 20 * 1024 * 1024;
                if (file.Size > maxFileSize)
                {
                    _errorMessage = "Il file supera la dimensione massima di 20MB";
                    return;
                }

                // Leggi il file
                byte[] fileBytes;
                using var stream = file.OpenReadStream(maxFileSize);
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();

                _selectedFile = new SelectedFileInfo
                {
                    Name = file.Name,
                    ContentType = file.ContentType,
                    Size = file.Size,
                    FileBytes = fileBytes
                };

                StateHasChanged();
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore durante la lettura del file: {ex.Message}";
                Console.WriteLine($"Errore OnInputFileChange: {ex}");
                StateHasChanged();
            }
        }

        private void RemoveFile()
        {
            _selectedFile = null;
            _errorMessage = string.Empty;
            _uploadSuccess = false;
            StateHasChanged();
        }

        private async Task HandleUpload()
        {
            if (_selectedFile == null)
            {
                _errorMessage = "Nessun file selezionato";
                return;
            }

            _errorMessage = string.Empty;
            _uploading = true;
            _uploadSuccess = false;
            StateHasChanged();

            try
            {
                // Prepara il modello per l'upload
                var uploadModel = new UploadFilesModel
                {
                    IdOwner = Id,
                    Files = new List<AttachmentFileModel>
                    {
                        new AttachmentFileModel
                        {
                            Content = Convert.ToBase64String(_selectedFile.FileBytes),
                            ContentType = _selectedFile.ContentType,
                            Id = 0
                        }
                    }
                };

                // Chiama il servizio
                bool result = await Service.UploadReport(Id, uploadModel);

                if (result)
                {
                    _uploadSuccess = true;
                    
                    NotificationService?.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Success,
                        Summary = "Upload completato",
                        Detail = "Il report è stato caricato con successo",
                        Duration = 4000
                    });

                    // Aspetta un po' per mostrare il messaggio di successo
                    await Task.Delay(1500);

                    // Callback o navigazione
                    if (OnUploadComplete != null)
                    {
                        OnUploadComplete(true);
                    }
                    else if (PageMode != PageModality.Dialog)
                    {
                        NavigationManager.NavigateTo($"/interventions/{Id}");
                    }
                }
                else
                {
                    _errorMessage = "Errore durante l'upload del report. Riprova.";
                    
                    NotificationService?.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Error,
                        Summary = "Errore Upload",
                        Detail = "Si è verificato un errore durante il caricamento",
                        Duration = 4000
                    });
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore durante l'upload: {ex.Message}";
                Console.WriteLine($"Errore HandleUpload: {ex}");
                
                NotificationService?.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = "Errore",
                    Detail = ex.Message,
                    Duration = 4000
                });
            }
            finally
            {
                _uploading = false;
                StateHasChanged();
            }
        }

        private void Cancel()
        {
            if (OnCancel != null)
            {
                OnCancel();
            }
            else if (PageMode != PageModality.Dialog)
            {
                NavigationManager.NavigateTo($"/interventions/{Id}");
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

        private class SelectedFileInfo
        {
            public string Name { get; set; }
            public string ContentType { get; set; }
            public long Size { get; set; }
            public byte[] FileBytes { get; set; }
        }
    }
}
