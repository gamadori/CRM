using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ExpenseReceipts
{
    [Authorize]
    public partial class Create : ComponentBase
    {
        /// <summary>
        /// Null quando si arriva dall'elenco globale: in quel caso il contesto lo sceglie
        /// l'operatore nel form, e "nessun contesto" e' una scelta valida.
        /// </summary>
        [Parameter] public int? InterventionId { get; set; }

        /// <summary>
        /// Valorizzato quando si arriva dalla visita in agenda. Alternativo all'intervento: sono
        /// due rotte diverse, e nessuna delle due chiede all'operatore di sapere un numero.
        /// </summary>
        [Parameter] public int? ActivityId { get; set; }

        /// <summary>Visita di riferimento; null su ogni altra rotta.</summary>
        private ActivityDTO _activity;

        /// <summary>Oggetto della visita, per intestare la pagina con qualcosa di leggibile.</summary>
        private string _activitySubject;

        /// <summary>Etichetta della visita: l'oggetto se lo conosciamo, altrimenti il numero.</summary>
        private string ActivityLabel => string.IsNullOrWhiteSpace(_activitySubject)
            ? $"Attivita #{ActivityId}"
            : _activitySubject;

        private string ContextTitle =>
            InterventionId.HasValue ? $"Spesa dell'intervento #{InterventionId}"
            : ActivityId.HasValue ? $"Spesa della visita: {ActivityLabel}"
            : "Costo generale";

        private string CancelUrl =>
            InterventionId.HasValue ? $"/TicketInterventions/{InterventionId}/ExpenseReceipts"
            : ActivityId.HasValue ? $"/Activities/{ActivityId}/Info?view=notespese"
            : "/ExpenseReceipts";

        private ExpenseReceiptCreateUpdateDTO _model = new();
        private ReceiptExtractionResult _extractionResult;
        private InputFile _inputFile;

        private bool _isLoading = false;
        private bool _isProcessing = false;
        private bool _isSaving = false;
        private string _errorMessage;

        private string _previewDataUrl;
        private string _previewContentType;
        private string _previewFileName;
        private int? _uploadedAttachmentFileId;

        /// <summary>Valute compatibili col simbolo letto, quando il codice ISO non c'e'.</summary>
        private List<string> _currencyCandidates = new();

        /// <summary>Persone selezionabili come chi ha sostenuto la spesa.</summary>
        private List<ApplicationUser> _users = new();
        private string _baseCurrency = "EUR";
        private bool _loadingRate;
        private bool _rateUnavailable;

        /// <summary>Il cambio serve solo se la spesa non e' gia' nella valuta di casa.</summary>
        private bool _needsRate =>
            !string.IsNullOrWhiteSpace(_model.Currency)
            && !string.Equals(_model.Currency.Trim(), _baseCurrency, StringComparison.OrdinalIgnoreCase);

        protected override async Task OnInitializedAsync()
        {
            _model.TicketInterventionId = InterventionId;
            _model.IdActivity = ActivityId;

            // La data e' obbligatoria: una spesa senza data non e' collocabile in nessun periodo,
            // quindi si parte da oggi invece che da vuoto.
            _model.TransactionDate = DateTime.Today;

            _activity = await LoadActivityAsync();
            _activitySubject = _activity?.Subject;

            // La spesa di una visita porta la data della visita, non quella in cui la si registra:
            // lo scontrino della trasferta finisce in ufficio giorni dopo. Se la visita e' stata
            // completata vale la data in cui e' avvenuta, non quella in cui era prevista.
            var visitDate = _activity?.DoneDate ?? _activity?.DueDate;
            if (visitDate.HasValue)
                _model.TransactionDate = visitDate.Value.Date;

            _baseCurrency = await LoadBaseCurrencyAsync();
            _model.Currency = _baseCurrency;

            await LoadSpendersAsync();
        }

        /// <summary>
        /// Elenco delle persone e preselezione.
        /// <para>
        /// L'elenco resta completo anche su un intervento: puo' aver pagato qualcuno che non e'
        /// fra gli assegnati. Cambia la PRESELEZIONE — su un intervento si punta a chi ci sta
        /// lavorando, altrove a chi sta registrando.
        /// </para>
        /// </summary>
        private async Task LoadSpendersAsync()
        {
            try
            {
                var response = await _userService.Get(new UsersFilterModel { PageSize = 0 });
                _users = response?.Items?.OrderBy(u => u.NameComplete).ToList() ?? new List<ApplicationUser>();

                var me = await Http.GetFromJsonAsync<ApplicationUser>("api/Users/CurrentUser");
                var myId = me?.Id;

                _model.IdUserSpender = await PickDefaultSpenderAsync(myId);
            }
            catch (Exception ex)
            {
                // Senza elenco il campo resta vuoto e la validazione blocca il salvataggio:
                // meglio fermarsi che attribuire la spesa a qualcuno a caso.
                Console.WriteLine($"Errore caricamento persone: {ex.Message}");
            }
        }

        /// <summary>
        /// Recupera la visita per intestazione, data e persona di riferimento. Un errore qui non
        /// deve impedire di registrare la spesa: si perde la precompilazione, non la funzione.
        /// </summary>
        private async Task<ActivityDTO> LoadActivityAsync()
        {
            if (!ActivityId.HasValue)
                return null;

            try
            {
                return await Http.GetFromJsonAsync<ActivityDTO>($"api/Activities/{ActivityId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Visita non caricata: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Su un intervento: chi registra, se e' fra gli assegnati; altrimenti il primo assegnato,
        /// che e' il caso dell'ufficio che inserisce la spesa del tecnico. Su una visita: chi ce
        /// l'ha in carico, con la stessa logica. Senza contesto: chi registra, perche' il caso
        /// normale e' "la spesa e' mia".
        /// </summary>
        private async Task<string> PickDefaultSpenderAsync(string currentUserId)
        {
            var fallback = _users.Any(u => u.Id == currentUserId) ? currentUserId : null;

            if (ActivityId.HasValue)
            {
                var owner = _activity?.IdAssignee ?? _activity?.IdUser;
                return _users.Any(u => u.Id == owner) ? owner : fallback;
            }

            if (!InterventionId.HasValue)
                return fallback;

            try
            {
                var assigned = await Http.GetFromJsonAsync<List<string>>(
                    $"api/TicketInterventionUsers/intervention/{InterventionId}/assigned-users");

                if (assigned == null || !assigned.Any())
                    return fallback;

                if (currentUserId != null && assigned.Contains(currentUserId))
                    return currentUserId;

                return assigned.FirstOrDefault(id => _users.Any(u => u.Id == id)) ?? fallback;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Assegnati dell'intervento non determinati: {ex.Message}");
                return fallback;
            }
        }

        private async Task<string> LoadBaseCurrencyAsync()
        {
            try
            {
                // Endpoint senza id: non presume che la riga delle impostazioni sia la numero 1.
                var settings = await Http.GetFromJsonAsync<CRM.Shared.GlobalSetting>("api/GlobalSettings");
                if (!string.IsNullOrWhiteSpace(settings?.BaseCurrency))
                    return settings.BaseCurrency.Trim().ToUpperInvariant();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Valuta base non determinata, si usa EUR: {ex.Message}");
            }

            return "EUR";
        }

        /// <summary>
        /// Conferma una delle valute compatibili col simbolo letto e chiede subito il cambio:
        /// scelta la valuta, il passo successivo e' sempre lo stesso.
        /// </summary>
        private async Task PickCurrency(string currency)
        {
            _model.Currency = currency;
            _currencyCandidates.Clear();
            await ProposeExchangeRate();
        }

        /// <summary>
        /// Chiede al server il cambio di riferimento per valuta e data. Resta una proposta:
        /// il campo e' modificabile perche' per un rimborso vale spesso il cambio della carta.
        /// </summary>
        private async Task ProposeExchangeRate()
        {
            _rateUnavailable = false;

            if (!_needsRate)
            {
                _model.ExchangeRate = null;
                return;
            }

            try
            {
                _loadingRate = true;
                StateHasChanged();

                var date = (_model.TransactionDate ?? DateTime.Today).ToString("yyyy-MM-dd");
                var currency = Uri.EscapeDataString(_model.Currency.Trim());

                var rate = await Http.GetFromJsonAsync<decimal?>($"api/ExpenseReceipts/exchange-rate?currency={currency}&date={date}");

                if (rate.HasValue && rate.Value > 0)
                    _model.ExchangeRate = rate;
                else
                    _rateUnavailable = true;
            }
            catch (Exception ex)
            {
                // Un cambio non disponibile non impedisce di salvare: la spesa resta da convertire.
                _rateUnavailable = true;
                Console.WriteLine($"Cambio non proposto: {ex.Message}");
            }
            finally
            {
                _loadingRate = false;
                StateHasChanged();
            }
        }

        private async Task OnFileSelected(InputFileChangeEventArgs e)
        {
            _errorMessage = null;
            var file = e.File;

            if (file.Size > 10 * 1024 * 1024)
            {
                _errorMessage = "File troppo grande. Massimo 10 MB.";
                return;
            }

            byte[] fileBytes;
            try
            {
                using var originalStream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
                using var memoryStream = new MemoryStream();
                await originalStream.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore lettura file: {ex.Message}";
                return;
            }

            var ct = file.ContentType ?? "application/octet-stream";
            _previewContentType = ct;
            _previewFileName = file.Name;
            _previewDataUrl = $"data:{ct};base64,{Convert.ToBase64String(fileBytes)}";

            await UploadAndProcessFile(fileBytes, file.Name, ct);
        }

        private async Task UploadAndProcessFile(byte[] fileBytes, string fileName, string contentType)
        {
            try
            {
                _isProcessing = true;
                StateHasChanged();

                // 1. Upload file as AttachmentFile
                var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                content.Add(fileContent, "file", fileName);

                // Senza intervento il file non ha un contenitore a cui appendersi: si usa la
                // rotta che ne crea uno intestato a chi carica.
                var uploadUrl = _model.TicketInterventionId.HasValue
                    ? $"api/AttachmentFiles/{_model.TicketInterventionId}/upload"
                    : "api/AttachmentFiles/upload";

                var uploadResponse = await Http.PostAsync(uploadUrl, content);

                if (!uploadResponse.IsSuccessStatusCode)
                {
                    _errorMessage = "Errore durante il caricamento del file.";
                    _isProcessing = false;
                    return;
                }

                var uploadedFile = await uploadResponse.Content.ReadFromJsonAsync<AttachmentFileUploadResult>();
                _uploadedAttachmentFileId = uploadedFile.Id;

                // 2. Process with Azure Form Recognizer
                var processResponse = await Http.GetFromJsonAsync<ReceiptExtractionResult>(
                    $"api/ReceiptProcessor/process/{_uploadedAttachmentFileId}");

                _extractionResult = processResponse;

                if (_extractionResult.Success)
                {
                    // Popola il modello con i dati estratti
                    _model.AttachmentFileId = _uploadedAttachmentFileId;
                    _model.MerchantName = _extractionResult.MerchantName;
                    _model.TransactionDate = _extractionResult.TransactionDate;
                    _model.TotalAmount = _extractionResult.TotalAmount;
                    _model.TaxAmount = _extractionResult.TaxAmount;
                    // Valuta non riconosciuta = campo vuoto da compilare, non "EUR" per default:
                    // su uno scontrino estero l'assunzione produrrebbe un importo sbagliato.
                    _model.Currency = _extractionResult.Currency;
                    _currencyCandidates = _extractionResult.CurrencyCandidates ?? new List<string>();
                    _model.Description = $"{_extractionResult.MerchantName} - {_extractionResult.TransactionDate?.ToString("dd/MM/yyyy")}";
                    _model.ExtractionConfidence = _extractionResult.AverageConfidence;
                    _model.ExtractedFieldsJson = JsonSerializer.Serialize(_extractionResult);
                    _model.Documents = ToDocumentDtos(_extractionResult);

                    // Se lo scontrino e' in valuta estera il cambio serve subito: si propone
                    // senza aspettare che l'operatore esca dal campo valuta.
                    if (_needsRate)
                        await ProposeExchangeRate();
                }
                else
                {
                    // Estrazione fallita, ma salva comunque l'attachment
                    _model.AttachmentFileId = _uploadedAttachmentFileId;
                    _errorMessage = _extractionResult.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore durante l'elaborazione: {ex.Message}";
                Console.Error.WriteLine($"Errore upload/process: {ex}");
            }
            finally
            {
                _isProcessing = false;
                StateHasChanged();
            }
        }

        private void ShowManualForm()
        {
            _extractionResult = new ReceiptExtractionResult { Success = false };
            _previewDataUrl = null;
        }

        private async Task SaveReceipt()
        {
            try
            {
                // Vale ovunque, non solo sul costo generale: senza chi ha speso la spesa sparisce
                // dai totali per persona, ed e' il rimborso a non tornare.
                if (string.IsNullOrWhiteSpace(_model.IdUserSpender))
                {
                    _errorMessage = "Indica chi ha sostenuto la spesa: serve per il rimborso e per i totali per persona.";
                    return;
                }

                _isSaving = true;

                var isBatch = _model.Documents?.Count > 1;
                var response = await Http.PostAsJsonAsync(
                    isBatch ? "api/ExpenseReceipts/batch" : "api/ExpenseReceipts",
                    _model);

                if (response.IsSuccessStatusCode)
                {
                    if (isBatch)
                    {
                        NavigationManager.NavigateTo(CancelUrl);
                        return;
                    }

                    var created = await response.Content.ReadFromJsonAsync<ExpenseReceiptDTO>();

                    // Si finisce sempre sul dettaglio di quello che si e' appena registrato, nel
                    // percorso da cui si e' arrivati quando ce n'e' uno.
                    if (created == null)
                        NavigationManager.NavigateTo(CancelUrl);
                    else if (created.TicketInterventionId != null)
                        NavigationManager.NavigateTo(
                            $"/TicketInterventions/{created.TicketInterventionId}/ExpenseReceipts/{created.Id}/Details");
                    else if (created.IdActivity != null)
                        NavigationManager.NavigateTo(
                            $"/Activities/{created.IdActivity}/ExpenseReceipts/{created.Id}/Details");
                    else
                        NavigationManager.NavigateTo($"/ExpenseReceipts/{created.Id}/Details");
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _errorMessage = string.IsNullOrWhiteSpace(body)
                        ? "Errore durante il salvataggio della nota spese."
                        : body;
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore: {ex.Message}";
                Console.Error.WriteLine($"Errore save receipt: {ex}");
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void ResetForm()
        {
            // "Ricarica altro" serve a inserire piu' scontrini di fila: chi ha speso quasi sempre
            // resta lo stesso, quindi si conserva invece di farlo riselezionare ogni volta.
            var previousSpender = _model.IdUserSpender;

            _model = new ExpenseReceiptCreateUpdateDTO
            {
                TicketInterventionId = InterventionId,
                IdActivity = ActivityId,
                IdUserSpender = previousSpender,
                Currency = _baseCurrency,
                TransactionDate = (_activity?.DoneDate ?? _activity?.DueDate)?.Date ?? DateTime.Today
            };
            _rateUnavailable = false;
            _currencyCandidates.Clear();
            _extractionResult = null;
            _previewDataUrl = null;
            _uploadedAttachmentFileId = null;
            _errorMessage = null;
        }

        private string GetConfidenceClass(float confidence)
        {
            if (confidence >= 0.9f) return "bg-success";
            if (confidence >= 0.7f) return "bg-warning";
            return "bg-danger";
        }

        private static List<ExpenseReceiptDocumentDTO> ToDocumentDtos(ReceiptExtractionResult extraction)
        {
            var documents = extraction.DocumentResults?.Count > 0
                ? extraction.DocumentResults
                : new List<ReceiptExtractionResult> { extraction };

            return documents.Select((document, documentIndex) => new ExpenseReceiptDocumentDTO
            {
                SortOrder = documentIndex,
                PageFrom = document.PageFrom,
                PageTo = document.PageTo,
                DocumentType = document.DocumentType,
                MerchantName = document.MerchantName,
                TransactionDate = document.TransactionDate,
                Currency = document.Currency,
                SubtotalAmount = document.SubtotalAmount,
                TaxAmount = document.TaxAmount,
                TotalAmount = document.TotalAmount,
                ExtractionConfidence = document.AverageConfidence,
                Lines = (document.LineItems ?? new()).Select((line, lineIndex) => new ExpenseReceiptDocumentLineDTO
                {
                    SortOrder = lineIndex,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    TotalPrice = line.TotalPrice,
                    ExtractionConfidence = line.Confidence
                }).ToList()
            }).ToList();
        }

        private static string FormatReceiptAmount(decimal? amount, string currency)
        {
            if (!amount.HasValue)
                return "-";

            var value = amount.Value.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
            return string.IsNullOrWhiteSpace(currency) ? value : $"{value} {currency}";
        }

        private class AttachmentFileUploadResult
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
