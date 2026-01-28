using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Client.Shared.Components;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
using Radzen;

namespace CRM.Client.Pages.TicketInterventions
{
    [Authorize]
    public partial class Details : ComponentBase
    {
        
        [Inject]
        private IBaseRestService<TicketIntervention, TicketInterventionFilter, int> _service { get; set; }

        [Inject]
        private HttpClient _httpClient { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public Action OnClickPdfViewer { get; set; }

        public Action OnClosePdfViewer { get; set; }

        [Parameter]
        public bool ViewCommands { get; set; } = true;

        protected enum MsgBoxComand
        {
            ViewReport,
            CreateReport,
            DownloadReport
        }

        
        private TicketIntervention _ticketIntervention = null;

        private string _typeMessage;
        private string _message = null;

        private string _msgBox;

        private MsgBoxComand _msgBoxComand;

        private bool _creatingReport = false;
        private bool _isSendingEmail = false;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
        }

        protected async Task LoadData()
        {
            try
            {
                _message = null;

                if (Id != null)
                {

                    _ticketIntervention = await _service.Get(Id.Value);
                }
                else
                    _ticketIntervention = new TicketIntervention();


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }


        private void Edit()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/TicketsIntervention/{Id}/Edit");
        }
        private void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo("/TicketsIntervention/Index");
        }

        private void SendInvitation()
        {

        }

        
        private async Task ReportCreate(string? languageCode = null)
        {
            await JSRuntime.InvokeVoidAsync("ShowModal", "dlgWaitingBox");
            
            _creatingReport = true;
            StateHasChanged();

            // Passa il parametro lingua all'API
            var url = $"{ConstHelper.TicketsInterventionsPath}/Report/{_ticketIntervention.Id}";
            if (!string.IsNullOrWhiteSpace(languageCode))
            {
                url += $"?languageCode={languageCode}";
            }

            var resp = await _httpClient.GetFromJsonAsync<bool>(url);

            await JSRuntime.InvokeVoidAsync("CloseModal", "dlgWaitingBox");

            if (resp)
            {
                _typeMessage = "alert-success";
                _message = "Report Creato con successo";

                await ReportViewAsync();
            }
            else
            {
                _typeMessage = "alert-danger";
                _message = "Report NON creato: Errore durante la sua creazione";

                await LoadData();
            }
            
            _creatingReport = false;
            StateHasChanged();
        }

        private async Task ReportViewAsync()
        {
            // Naviga direttamente alla pagina ReportViewer invece di aprire una dialog
            NavigationManager.NavigateTo($"/TicketInterventions/ReportViewer/{_ticketIntervention.Id}");
        }

        private async void ReportView()
        {
            await ReportViewAsync();
        }

        private async Task CreateReportPrepare()
        {
            if (_ticketIntervention == null)
                return;

            // Mostra dialog selezione lingua
            var selectedLanguageCode = await DialogService.OpenAsync<LanguageSelectionDialog>(
                Localize["Select Report Language"],
                null,
                new DialogOptions 
                { 
                    Width = "600px", 
                    Height = "auto",
                    CloseDialogOnOverlayClick = false
                }
            );

            // Se l'utente ha annullato, esci
            if (selectedLanguageCode == null || string.IsNullOrWhiteSpace(selectedLanguageCode.ToString()))
            {
                return;
            }

            // Mostra conferma se report già esistente
            if (_ticketIntervention.AttachmentExist)
                _msgBox = Localize["AttentionReportAlreadyPresent"];
            else
                _msgBox = Localize["Creare il Report di intervento?"];

            if (await DialogService.Confirm(_msgBox) == true)
            {
                await ReportCreate(selectedLanguageCode.ToString());
            }
            

        }





        private async Task DownloadReport()
        {

            if (await DialogService.Confirm(Localize["Download Report?"]) == true)
            {
                var response = await _httpClient.GetAsync($"{ConstHelper.TicketsInterventionsPath}/Download/{Id}");

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
        }
        private async void ReportUploadDialog()
        {
            await JSRuntime.InvokeVoidAsync("ShowModal", "dlgUploadFile");
        }

        private async Task<bool> UploadReport(UploadFilesModel file)
        {
            var resp = await _httpClient.PostAsJsonAsync<UploadFilesModel>($"{ConstHelper.TicketsInterventionsPath}/UploadReport/{Id}", file);

            return resp.StatusCode == System.Net.HttpStatusCode.OK;
        }

        private async Task OnUploaded(bool state)
        {
            if (state)
            {
                await JSRuntime.InvokeVoidAsync("CloseModal", "dlgUploadFile");
                StateHasChanged();
                

            }
        }
        private async void CloseUploadReport()
        {
            await JSRuntime.InvokeVoidAsync("CloseModal", "dlgUploadFile");
        }

        private void CloseReportView()
        {

        }

        /// <summary>
        /// Apre dialog per rinviare email di conferma firma
        /// </summary>
        private async Task OpenResendConfirmationDialog()
        {
            if (_ticketIntervention == null) return;

            var result = await DialogService.OpenAsync<ResendConfirmationEmailDialog>(
                "Rinvia Email Conferma Firma",
                new Dictionary<string, object>
                {
                    { "CurrentEmail", _ticketIntervention.SignatureEmail ?? string.Empty },
                    { "SignerName", _ticketIntervention.SignatureName ?? string.Empty }
                },
                new DialogOptions { Width = "500px", Height = "auto" }
            );

            if (result is string newEmail && !string.IsNullOrWhiteSpace(newEmail))
            {
                await ResendConfirmationEmail(newEmail);
            }
        }

        /// <summary>
        /// Rinvia l'email di conferma firma
        /// </summary>
        private async Task ResendConfirmationEmail(string email)
        {
            _isSendingEmail = true;
            StateHasChanged();

            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"{ConstHelper.TicketsInterventionsPath}/ResendSignatureConfirmation/{_ticketIntervention.Id}",
                    new { Email = email }
                );

                if (response.IsSuccessStatusCode)
                {
                    // Aggiorna email nell'oggetto corrente
                    _ticketIntervention.SignatureEmail = email;
                    
                    await DialogService.Alert(
                        $"Email di conferma rinviata con successo a: {email}",
                        "Successo",
                        new AlertOptions { OkButtonText = "OK" }
                    );
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    await DialogService.Alert(
                        $"Errore durante l'invio: {error}",
                        "Errore",
                        new AlertOptions { OkButtonText = "OK" }
                    );
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore rinvio email: {ex.Message}");
                await DialogService.Alert(
                    $"Errore: {ex.Message}",
                    "Errore",
                    new AlertOptions { OkButtonText = "OK" }
                );
            }
            finally
            {
                _isSendingEmail = false;
                StateHasChanged();
            }
        }
    }
}
