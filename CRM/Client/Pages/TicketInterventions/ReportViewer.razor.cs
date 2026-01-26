using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Client.Shared.Components;
using CRM.Shared;
using CRM.Shared.Resources.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketInterventions
{
    public partial class ReportViewer : ComponentBase, IAsyncDisposable
    {
        [Inject]
        private IJSRuntime JS { get; set; } = default!;

        [Inject]
        private IBaseRestService<TicketIntervention, TicketInterventionFilter, int> _service { get; set; }

        [Inject]
        private HttpClient Http { get; set; } = default!;

        [Inject]
        private DialogService DialogService { get; set; } = default!;

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; } = default!;

        [Inject]
        private NavigationManager NavigationManager { get; set; } = default!;

        [Parameter]
        public int Id { get; set; }

        private bool _loaded;
        private string _loadingMessage = "Caricamento report...";
        private bool _showSignatureOverlay;
        private bool _hasSignature;
        private bool _isSigning;
        private bool _isSending;
        private string _signatureStatus = string.Empty;
        private string _signatureEmail = string.Empty;
        private string _signerName = string.Empty;
        private TicketIntervention? _ticketIntervention = null;

        private ElementReference containerRef;
        private ElementReference pdfHostRef;
        private bool _initialized;
        
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _ticketIntervention = await _service.Get(Id);
                _initialized = true;
                await LoadReport();
                await CheckSignature();
                StateHasChanged();
            }
        }

        private async Task LoadReport()
        {
            try
            {
                _loaded = false;
                _loadingMessage = "Caricamento report...";
                StateHasChanged();

                var base64Pdf = await Http.GetStringAsync($"{ConstHelper.TicketsInterventionsPath}/getreport/{Id}");

                if (string.IsNullOrEmpty(base64Pdf))
                {
                    _loadingMessage = "Nessun report disponibile";
                    _loaded = true;
                    StateHasChanged();
                    return;
                }

                _loadingMessage = "Rendering PDF...";
                StateHasChanged();

                var pdfBytes = Convert.FromBase64String(base64Pdf);
                await JS.InvokeVoidAsync("displayFileInElement", pdfHostRef, "application/pdf", pdfBytes, $"report_{Id}.pdf");

                _loaded = true;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore caricamento report: {ex.Message}");
                _loadingMessage = $"Errore: {ex.Message}";
                _loaded = true;
                StateHasChanged();
            }
        }

        private void GoToDetails()
        {
            if (_ticketIntervention != null)
                NavigationManager.NavigateTo($"Tickets/{_ticketIntervention.IdTicket}/intervention/{Id}");
            else
                NavigationManager.NavigateTo($"/TicketsIntervention/{Id}");
        }

        private async Task ReportUploadDialog()
        {
            await JS.InvokeVoidAsync("ShowModal", "dlgUploadFile");
        }

        private async Task<bool> UploadReport(UploadFilesModel file)
        {
            try
            {
                var response = await Http.PostAsJsonAsync($"{ConstHelper.TicketsInterventionsPath}/UploadReport/{Id}", file);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore upload: {ex.Message}");
                return false;
            }
        }

        private async Task OnReportUploaded(bool success)
        {
            if (success)
            {
                await JS.InvokeVoidAsync("CloseModal", "dlgUploadFile");
                await LoadReport();
            }
        }

        private async Task CloseUploadReport()
        {
            await JS.InvokeVoidAsync("CloseModal", "dlgUploadFile");
        }

        private async Task ReportCreatePrepare()
        {
            var message = Localize["Ricreare il report? Il report esistente sarà sovrascritto."];
            
            if (await DialogService.Confirm(message, Localize["Conferma"], new ConfirmOptions { OkButtonText = "Sì", CancelButtonText = "No" }) == true)
            {
                await ReportCreate();
            }
        }

        private async Task ReportCreate()
        {
            try
            {
                _loaded = false;
                _loadingMessage = "Generazione report in corso...";
                StateHasChanged();

                var success = await Http.GetFromJsonAsync<bool>($"{ConstHelper.TicketsInterventionsPath}/Report/{Id}");

                if (success)
                {
                    await LoadReport();
                }
                else
                {
                    _loadingMessage = "Errore durante la generazione del report";
                    _loaded = true;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore creazione report: {ex.Message}");
                _loadingMessage = $"Errore: {ex.Message}";
                _loaded = true;
                StateHasChanged();
            }
        }

        private async Task DownloadReport()
        {
            try
            {
                if (await DialogService.Confirm(Localize["Download Report?"], Localize["Conferma"]) != true)
                    return;

                var response = await Http.GetAsync($"{ConstHelper.TicketsInterventionsPath}/Download/{Id}");

                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();

                    var header = JsonSerializer.Deserialize<AttachmentResponse>(
                        response.Headers.GetValues(ConstHelper.FileHeader).First(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    await JS.InvokeVoidAsync(
                        "downloadFromByteArray",
                        new
                        {
                            ByteArray = bytes,
                            FileName = header?.Name ?? $"Report_{Id}.pdf",
                            ContentType = header?.ContentType ?? "application/pdf"
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore download: {ex.Message}");
                await DialogService.Alert(Localize["Errore durante il download"], Localize["Errore"]);
            }
        }

        private async Task OpenSendEmail()
        {
            await DialogService.OpenAsync<EmailSender>(
                Localize["Invio Report"],
                new Dictionary<string, object>
                {
                    { "SendEmailApiUrl", $"{ConstHelper.TicketsInterventionsPath}/Email/{Id}" },
                    { "EmailAddressesApiUrl", $"{ConstHelper.TicketsInterventionsPath}/CompanyEmailAdresses/{Id}" }
                },
                new DialogOptions { Height = "auto", Width = "50%" }
            );
        }

        private async Task OpenEmailSent()
        {
            await DialogService.OpenAsync<CRM.Client.Pages.EmailsSent.Info>(
                Localize["Email Inviate"],
                null,
                new DialogOptions { Height = "auto", Style = "min-height:400px" }
            );
        }

        private void Close()
        {
            GoToDetails();
        }

        private async Task CheckSignature()
        {
            try
            {
                // Ottieni dettagli intervento per verificare stato firma
                var intervention = await Http.GetFromJsonAsync<TicketIntervention>(
                    $"{ConstHelper.TicketsInterventionsPath}/{Id}"
                );

                if (intervention != null)
                {
                    _hasSignature = !string.IsNullOrWhiteSpace(intervention.CustomerSignature);
                    _signatureStatus = intervention.SignatureStatus.ToString();
                    _signatureEmail = intervention.SignatureEmail ?? string.Empty;
                    _signerName = intervention.SignatureName ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore verifica firma: {ex.Message}");
                _hasSignature = false;
                _signatureStatus = string.Empty;
                _signatureEmail = string.Empty;
            }
        }

        /// <summary>
        /// Apre dialog per rinviare email di conferma (con possibilità di modificare l'email)
        /// </summary>
        private async Task OpenResendConfirmationDialog()
        {
            var result = await DialogService.OpenAsync<ResendConfirmationEmailDialog>(
                "Rinvia Email Conferma Firma",
                new Dictionary<string, object>
                {
                    { "CurrentEmail", _signatureEmail },
                    { "SignerName", _signerName }
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
            _isSending = true;
            StateHasChanged();

            try
            {
                var response = await Http.PostAsJsonAsync(
                    $"{ConstHelper.TicketsInterventionsPath}/ResendSignatureConfirmation/{Id}",
                    new { Email = email }
                );

                if (response.IsSuccessStatusCode)
                {
                    _signatureEmail = email; // Aggiorna email visualizzata
                    
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
                _isSending = false;
                StateHasChanged();
            }
        }

        private async Task OpenSignatureOverlay()
        {
            _showSignatureOverlay = true;
            StateHasChanged();
        }

        private async Task OnSignatureSaved(string signatureBase64)
        {
            _showSignatureOverlay = false;
            _isSigning = true;
            _loadingMessage = "Salvataggio firma e rigenerazione PDF...";
            _loaded = false;
            StateHasChanged();

            try
            {
                var saveResponse = await Http.PostAsJsonAsync(
                    $"{ConstHelper.TicketsInterventionsPath}/SaveSignature/{Id}",
                    signatureBase64
                );

                if (!saveResponse.IsSuccessStatusCode)
                {
                    _loadingMessage = "Errore salvataggio firma";
                    _loaded = true;
                    _isSigning = false;
                    StateHasChanged();
                    return;
                }

                _loadingMessage = "Rigenerazione PDF con firma...";
                StateHasChanged();

                var regenerateSuccess = await Http.GetFromJsonAsync<bool>(
                    $"{ConstHelper.TicketsInterventionsPath}/Report/{Id}"
                );

                if (regenerateSuccess)
                {
                    _hasSignature = true;
                    await LoadReport();
                    
                    await DialogService.Alert(
                        "Firma salvata e PDF rigenerato con successo!",
                        "Successo",
                        new AlertOptions { OkButtonText = "OK" }
                    );
                }
                else
                {
                    _loadingMessage = "Errore rigenerazione PDF";
                    _loaded = true;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore salvataggio firma: {ex.Message}");
                _loadingMessage = $"Errore: {ex.Message}";
                _loaded = true;
                
                await DialogService.Alert(
                    $"Errore durante il salvataggio: {ex.Message}",
                    "Errore",
                    new AlertOptions { OkButtonText = "OK" }
                );
            }
            finally
            {
                _isSigning = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Callback quando firma viene salvata
        /// </summary>
        private async Task OnSignatureWithNameSaved((string signature, string signerName) data)
        {
            _showSignatureOverlay = false;
            _isSigning = true;
            _loadingMessage = "Salvataggio firma e rigenerazione PDF...";
            _loaded = false;
            StateHasChanged();

            try
            {
                // 1. Prepara oggetto firma con nome
                var signatureData = new
                {
                    Signature = data.signature,
                    SignerName = data.signerName
                };

                // 2. Salva la firma con nome
                var saveResponse = await Http.PostAsJsonAsync(
                    $"{ConstHelper.TicketsInterventionsPath}/SaveSignature/{Id}",
                    signatureData
                );

                if (!saveResponse.IsSuccessStatusCode)
                {
                    _loadingMessage = "Errore salvataggio firma";
                    _loaded = true;
                    _isSigning = false;
                    StateHasChanged();
                    return;
                }

                _loadingMessage = "Rigenerazione PDF con firma...";
                StateHasChanged();

                // 3. Rigenera il PDF
                var regenerateSuccess = await Http.GetFromJsonAsync<bool>(
                    $"{ConstHelper.TicketsInterventionsPath}/Report/{Id}"
                );

                if (regenerateSuccess)
                {
                    _hasSignature = true;
                    await LoadReport();
                    
                    await DialogService.Alert(
                        $"Firma di {data.signerName} salvata con successo!",
                        "Successo",
                        new AlertOptions { OkButtonText = "OK" }
                    );
                }
                else
                {
                    _loadingMessage = "Errore rigenerazione PDF";
                    _loaded = true;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore salvataggio firma: {ex.Message}");
                _loadingMessage = $"Errore: {ex.Message}";
                _loaded = true;
                
                await DialogService.Alert(
                    $"Errore durante il salvataggio: {ex.Message}",
                    "Errore",
                    new AlertOptions { OkButtonText = "OK" }
                );
            }
            finally
            {
                _isSigning = false;
                StateHasChanged();
            }
        }

        private void OnSignatureCancelled()
        {
            _showSignatureOverlay = false;
            StateHasChanged();
        }

        /// <summary>
        /// Callback quando firma richiede conferma email (Opzione 3)
        /// </summary>
        private async Task OnSignatureWithEmailConfirmation((string signature, string signerName, string signerEmail) data)
        {
            _showSignatureOverlay = false;
            _loadingMessage = "Salvataggio firma e invio email conferma...";
            _loaded = false;
            StateHasChanged();

            try
            {
                // 1. Salva firma con email di conferma
                var signatureData = new
                {
                    Signature = data.signature,
                    SignerName = data.signerName,
                    SignerEmail = data.signerEmail
                };

                var response = await Http.PostAsJsonAsync(
                    $"{ConstHelper.TicketsInterventionsPath}/SaveSignatureWithEmailConfirmation/{Id}",
                    signatureData
                );

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _loadingMessage = $"Errore salvataggio firma: {errorContent}";
                    _loaded = true;
                    StateHasChanged();
                    return;
                }

                var saveResponse = await response.Content.ReadFromJsonAsync<SignatureSaveResponse>();

                if (saveResponse != null && saveResponse.Success)
                {
                    // 2. Rigenera PDF
                    _hasSignature = true;
                    _loadingMessage = "Rigenerazione PDF con firma...";
                    StateHasChanged();

                    var regenerateSuccess = await Http.GetFromJsonAsync<bool>(
                        $"{ConstHelper.TicketsInterventionsPath}/Report/{Id}"
                    );

                    if (regenerateSuccess)
                    {
                        await LoadReport();
                        
                        await DialogService.Alert(
                            $@"? Firma salvata con successo!

?? Email di conferma inviata a: {data.signerEmail}

?? La firma sarà valida SOLO dopo la conferma tramite email.

Il cliente riceverà un'email con un link per confermare la firma.",
                            "Firma Salvata - Conferma Richiesta",
                            new AlertOptions { OkButtonText = "OK" }
                        );
                    }
                    else
                    {
                        _loadingMessage = "Errore rigenerazione PDF";
                        _loaded = true;
                    }
                }
                else
                {
                    _loadingMessage = "Errore salvataggio firma";
                    _loaded = true;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore salvataggio firma: {ex.Message}");
                _loadingMessage = $"Errore: {ex.Message}";
                _loaded = true;
                
                await DialogService.Alert(
                    $"Errore durante il salvataggio: {ex.Message}",
                    "Errore",
                    new AlertOptions { OkButtonText = "OK" }
                );
            }
            finally
            {
                StateHasChanged();
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await JS.InvokeVoidAsync("cleanupFileHost", pdfHostRef);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore dispose: {ex.Message}");
            }
        }

        // ? DTO per risposta salvataggio firma
        private class SignatureSaveResponse
        {
            public bool Success { get; set; }
            public string Status { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public bool ConfirmationRequired { get; set; }
        }
    }
}
