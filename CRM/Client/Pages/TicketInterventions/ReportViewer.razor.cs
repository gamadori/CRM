using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Client.Shared.Components;
using CRM.Client.Pages.TicketInterventions.Components;
using CRM.Shared;
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

        [Inject]
        private IAGRestClientService RestClientServer { get; set; } = default!;

        [Parameter]
        public int Id { get; set; }

        // Modalità di raccolta firma per questo intervento.
        private enum SignatureMode { None, OnSite, Remote }
        private SignatureMode _signatureMode = SignatureMode.None;
        private bool _remoteSignatureEnabled;
        private bool _isSendingRemote;

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

        // --- Stato flusso OTP (codice via SMS/email) ---
        private bool _showOtpModal;
        private string _otpChallengeId = string.Empty;
        private DateTime _otpExpiresAt;
        private string _otpSentTo = string.Empty;
        private OtpModal? _otpModal;
        private CRM.Shared.Models.SignaturePendingData? _pendingOtp;

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
                NavigationManager.NavigateTo($"Tickets/{_ticketIntervention.IdTicket}/interventions/{Id}");
            else
                NavigationManager.NavigateTo($"/TicketsInterventions/{Id}");
        }

        private async Task ReportUploadDialog()
        {
            var result = await DialogService.OpenAsync<UploadReportDialog>(
                "Upload Report Intervento",
                new Dictionary<string, object>
                {
                    { "InterventionId", Id }
                },
                new DialogOptions 
                { 
                    Width = "600px", 
                    Height = "auto",
                    CloseDialogOnOverlayClick = false
                }
            );

            if (result is bool success && success)
            {
                // Ricarica il report
                _loaded = false;
                _loadingMessage = "Ricaricamento report...";
                StateHasChanged();
                
                await LoadReport();
            }
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

            // Mostra conferma
            var message = Localize["Ricreare il report? Il report esistente sarà sovrascritto."];
            
            if (await DialogService.Confirm(message, Localize["Conferma"], new ConfirmOptions { OkButtonText = "Sì", CancelButtonText = "No" }) == true)
            {
                await ReportCreate(selectedLanguageCode.ToString());
            }
        }

        private async Task ReportCreate(string? languageCode = null)
        {
            try
            {
                _loaded = false;
                _loadingMessage = "Generazione report in corso...";
                StateHasChanged();

                // Passa il parametro lingua all'API
                var url = $"{ConstHelper.TicketsInterventionsPath}/Report/{Id}";
                if (!string.IsNullOrWhiteSpace(languageCode))
                {
                    url += $"?languageCode={languageCode}";
                }

                var success = await Http.GetFromJsonAsync<bool>(url);

                if (success)
                {
                    await LoadReport();
                    
                    await DialogService.Alert(
                        Localize["Report creato con successo"],
                        Localize["Successo"],
                        new AlertOptions { OkButtonText = "OK" }
                    );
                }
                else
                {
                    _loadingMessage = "Errore durante la generazione del report";
                    _loaded = true;
                    StateHasChanged();
                    
                    await DialogService.Alert(
                        Localize["Errore durante la generazione del report"],
                        Localize["Errore"],
                        new AlertOptions { OkButtonText = "OK" }
                    );
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore creazione report: {ex.Message}");
                _loadingMessage = $"Errore: {ex.Message}";
                _loaded = true;
                StateHasChanged();
                
                await DialogService.Alert(
                    $"{Localize["Errore"]}: {ex.Message}",
                    Localize["Errore"],
                    new AlertOptions { OkButtonText = "OK" }
                );
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

                    await ComputeSignatureModeAsync(intervention.SignatureRequirement);
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
        /// Come si raccoglie la firma di questo intervento. Il requisito e' quello congelato
        /// sull'intervento alla sua creazione, non piu' un ramo dedotto dal tipo: cosi' un verbale
        /// vecchio continua a comportarsi come il giorno in cui e' stato scritto.
        /// <para>
        /// Con la firma sul dispositivo resta comunque disponibile la richiesta da remoto: e' il
        /// caso del cliente che se n'e' andato prima che il tecnico chiudesse il verbale. Il
        /// ripiego dipende pero' dall'interruttore generale, che spegne il canale remoto.
        /// </para>
        /// </summary>
        private async Task ComputeSignatureModeAsync(SignatureRequirement requirement)
        {
            try
            {
                var settings = await RestClientServer.GetFirst<GlobalSetting>(ConstHelper.GlobalSettingsPath);
                _remoteSignatureEnabled = settings?.RemoteSignatureEnabled ?? false;
            }
            catch
            {
                _remoteSignatureEnabled = false;
            }

            _signatureMode = requirement switch
            {
                SignatureRequirement.OnDevice => SignatureMode.OnSite,
                SignatureRequirement.Remote when _remoteSignatureEnabled => SignatureMode.Remote,
                _ => SignatureMode.None
            };
        }

        /// <summary>Il ripiego remoto si offre dove la firma sul dispositivo non si e' potuta prendere.</summary>
        private bool CanFallBackToRemote
            => _signatureMode == SignatureMode.OnSite && _remoteSignatureEnabled && !_hasSignature;

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

        /// <summary>
        /// La firma è pronta: richiede l'invio di un codice OTP al firmatario
        /// (SMS se ha indicato il cellulare, altrimenti email) e apre la modale.
        /// </summary>
        private async Task OnOtpSignatureRequested((string signature, string signerName, string signerEmail, string signerPhone) data)
        {
            _showSignatureOverlay = false;
            _loadingMessage = "Invio del codice di verifica...";
            _loaded = false;
            StateHasChanged();

            try
            {
                _pendingOtp = new CRM.Shared.Models.SignaturePendingData
                {
                    Signature = data.signature,
                    SignerName = data.signerName,
                    SignerEmail = data.signerEmail,
                    SignerPhone = data.signerPhone
                };

                var (otpResp, error) = await RequestOtpAsync();
                _loaded = true;

                if (otpResp != null && otpResp.Success)
                {
                    _otpChallengeId = otpResp.ChallengeId;
                    _otpExpiresAt = otpResp.ExpiresAt;
                    _otpSentTo = otpResp.SentTo;
                    _showOtpModal = true;
                }
                else
                {
                    await DialogService.Alert(
                        error ?? "Impossibile inviare il codice di verifica. Riprova.",
                        "Errore", new AlertOptions { OkButtonText = "OK" });
                }
            }
            catch (Exception ex)
            {
                _loaded = true;
                await DialogService.Alert(
                    $"Errore durante l'invio del codice: {ex.Message}",
                    "Errore", new AlertOptions { OkButtonText = "OK" });
            }
            finally
            {
                StateHasChanged();
            }
        }

        /// <summary>Codice inserito nella modale: lo verifica e finalizza la firma.</summary>
        private async Task OnOtpEntered(string otp)
        {
            try
            {
                var payload = new CRM.Shared.Models.OtpVerifyRequest
                {
                    ChallengeId = _otpChallengeId,
                    Otp = otp
                };

                var resp = await Http.PostAsJsonAsync(
                    $"{ConstHelper.TicketsInterventionsPath}/VerifySignatureOtp/{Id}", payload);

                if (resp.IsSuccessStatusCode)
                {
                    _showOtpModal = false;
                    _hasSignature = true;
                    _signatureStatus = "Verified";
                    _loadingMessage = "Rigenerazione PDF con firma...";
                    _loaded = false;
                    StateHasChanged();

                    var regenerated = await Http.GetFromJsonAsync<bool>(
                        $"{ConstHelper.TicketsInterventionsPath}/Report/{Id}");

                    if (regenerated)
                    {
                        await LoadReport();
                        await DialogService.Alert(
                            "Firma verificata e salvata con successo.",
                            "Firma Confermata", new AlertOptions { OkButtonText = "OK" });
                    }
                    else
                    {
                        _loadingMessage = "Errore rigenerazione PDF";
                        _loaded = true;
                    }

                    StateHasChanged();
                    return;
                }

                // Codice errato/scaduto: mostra il messaggio nella modale
                var (message, remaining) = await ParseOtpErrorAsync(resp);
                _otpModal?.SetError(message, remaining);
            }
            catch (Exception ex)
            {
                _otpModal?.SetError($"Errore di verifica: {ex.Message}");
            }
        }

        /// <summary>Richiede un nuovo codice riusando gli stessi dati di firma.</summary>
        private async Task OnOtpResend()
        {
            try
            {
                var (otpResp, error) = await RequestOtpAsync();
                if (otpResp != null && otpResp.Success)
                {
                    _otpChallengeId = otpResp.ChallengeId;
                    _otpExpiresAt = otpResp.ExpiresAt;
                    _otpSentTo = otpResp.SentTo;
                    StateHasChanged();
                }
                else
                {
                    _otpModal?.SetError(error ?? "Impossibile reinviare il codice.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore reinvio OTP: {ex.Message}");
            }
        }

        private void OnOtpCancelled()
        {
            _showOtpModal = false;
            StateHasChanged();
        }

        /// <summary>
        /// Interventi da remoto: invia al cliente un link per firmare dal proprio
        /// dispositivo (pagina pubblica /RemoteSignature).
        /// </summary>
        private async Task RequestRemoteSignature()
        {
            // Il recapito si vede e si corregge prima di partire: il link non se ne va piu' da
            // solo verso l'indirizzo generico dell'azienda senza che nessuno lo sappia.
            var scelta = await DialogService.OpenAsync<RemoteSignatureRecipientDialog>(
                "Richiedi firma al cliente",
                new Dictionary<string, object>
                {
                    { "Email", _signatureEmail ?? string.Empty },
                    { "AlreadySigned", _hasSignature && _signatureStatus == nameof(CRM.Shared.SignatureStatus.Verified) }
                },
                new DialogOptions { Width = "520px", Height = "auto", CloseDialogOnEsc = true });

            if (scelta is not CRM.Shared.Models.RemoteSignatureRequest request)
                return;

            _isSendingRemote = true;
            StateHasChanged();

            try
            {
                var resp = await Http.PostAsJsonAsync(
                    $"{ConstHelper.TicketsInterventionsPath}/RequestRemoteSignature/{Id}", request);

                if (resp.IsSuccessStatusCode)
                {
                    var result = await resp.Content.ReadFromJsonAsync<CRM.Shared.Models.RemoteSignatureRequestResponse>();
                    await CheckSignature();

                    var via = result?.Channel == "sms" ? "SMS" : "email";
                    await DialogService.Alert(
                        $"Link di firma inviato al cliente via {via} a {result?.SentTo}. Il documento risulterà firmato quando il cliente completa la firma dal link.",
                        "Richiesta inviata", new AlertOptions { OkButtonText = "OK" });
                }
                else
                {
                    var (message, _) = await ParseOtpErrorAsync(resp);
                    await DialogService.Alert(message, "Errore", new AlertOptions { OkButtonText = "OK" });
                }
            }
            catch (Exception ex)
            {
                await DialogService.Alert($"Errore invio link: {ex.Message}", "Errore", new AlertOptions { OkButtonText = "OK" });
            }
            finally
            {
                _isSendingRemote = false;
                StateHasChanged();
            }
        }

        private async Task<(CRM.Shared.Models.OtpRequestResponse? data, string? error)> RequestOtpAsync()
        {
            if (_pendingOtp == null)
                return (null, "Dati della firma mancanti.");

            var resp = await Http.PostAsJsonAsync(
                $"{ConstHelper.TicketsInterventionsPath}/RequestSignatureOtp/{Id}", _pendingOtp);

            if (resp.IsSuccessStatusCode)
                return (await resp.Content.ReadFromJsonAsync<CRM.Shared.Models.OtpRequestResponse>(), null);

            // Estrae il messaggio d'errore del server (es. "Riprova tra 45 secondi").
            var error = $"Errore {(int)resp.StatusCode}";
            try
            {
                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                    error = e.GetString() ?? error;
            }
            catch { /* corpo non JSON */ }

            return (null, error);
        }

        private static async Task<(string message, int remaining)> ParseOtpErrorAsync(HttpResponseMessage resp)
        {
            var message = (int)resp.StatusCode switch
            {
                401 => "Codice non valido o scaduto",
                423 => "Troppi tentativi. Richiedi un nuovo codice",
                _ => "Verifica non riuscita"
            };
            int remaining = 3;

            try
            {
                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                    message = err.GetString() ?? message;
                if (doc.RootElement.TryGetProperty("attemptsRemaining", out var rem) && rem.TryGetInt32(out var r))
                    remaining = r;
            }
            catch { /* corpo non JSON: usa il messaggio di default */ }

            return (message, remaining);
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
