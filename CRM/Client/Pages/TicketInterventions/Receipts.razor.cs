using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketInterventions
{
    [Authorize]
    public partial class Receipts : ComponentBase
    {
        [Inject] private ITicketInterventionsService InterventionService { get; set; }

        [Parameter] public int Id { get; set; }

        private TicketIntervention _intervention;
        private ReceiptExtractionResult _extractionResult;
        private InputFile _inputFile;

        private bool _isLoading = true;
        private bool _isProcessing = false;
        private bool _isSaving = false;
        private string _errorMessage;

        // Preview del documento caricato
        private string _previewDataUrl;
        private string _previewContentType;
        private string _previewFileName;

        // Campi editabili dopo estrazione
        private decimal? _editAmount;
        private decimal? _editTax;
        private DateTime? _editDate;
        private string _editMerchant;
        private string _editDescription;

        protected override async Task OnInitializedAsync()
        {
            await LoadIntervention();
            _isLoading = false;
        }

        private async Task LoadIntervention()
        {
            try
            {
                _intervention = await InterventionService.Get(Id);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore caricamento intervento: {ex.Message}");
            }
        }

        private void TriggerFileInput()
        {
            // Il click sulla label gestisce l'apertura del file picker
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

            // Read bytes immediately before any await that yields control,
            // because Blazor invalidates the IBrowserFile reference after the event handler completes.
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

            // Build a data URL for inline preview
            var ct = file.ContentType ?? "application/octet-stream";
            _previewContentType = ct;
            _previewFileName = file.Name;
            _previewDataUrl = $"data:{ct};base64,{Convert.ToBase64String(fileBytes)}";

            await ProcessFile(fileBytes, file.Name, ct);
        }

        private async Task ProcessFile(byte[] fileBytes, string fileName, string contentType)
        {
            _isProcessing = true;
            _errorMessage = null;
            StateHasChanged();

            try
            {
                using var content = new MultipartFormDataContent();
                using var byteContent = new ByteArrayContent(fileBytes);
                byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    contentType ?? "application/octet-stream");
                content.Add(byteContent, "file", fileName);

                var response = await Http.PostAsync("api/ReceiptProcessor/upload-and-process", content);

                if (response.IsSuccessStatusCode)
                {
                    _extractionResult = await response.Content.ReadFromJsonAsync<ReceiptExtractionResult>();

                    if (_extractionResult?.Success == true)
                    {
                        // Pre-popola i campi editabili con i valori estratti
                        _editAmount = _extractionResult.TotalAmount;
                        _editTax = _extractionResult.TaxAmount;
                        _editDate = _extractionResult.TransactionDate;
                        _editMerchant = _extractionResult.MerchantName;
                        _editDescription = _extractionResult.Description;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _errorMessage = $"Errore elaborazione: {response.StatusCode} - {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore: {ex.Message}";
            }
            finally
            {
                _isProcessing = false;
                StateHasChanged();
            }
        }

        private async Task ConfirmAndSave()
        {
            if (_intervention == null) return;

            _isSaving = true;
            StateHasChanged();

            try
            {
                _intervention.ExtractedTotalAmount = _editAmount;
                _intervention.ExtractedTaxAmount = _editTax;
                _intervention.ExtractedTransactionDate = _editDate;
                _intervention.ExtractedMerchantName = _editMerchant;
                _intervention.ExtractedDescription = _editDescription;
                _intervention.ExtractedCurrency = _extractionResult?.Currency ?? "EUR";
                _intervention.ExtractionConfidence = _extractionResult?.AverageConfidence;
                _intervention.ReceiptProcessedDate = DateTime.Now;
                _intervention.ExtractionConfirmed = true;
                _intervention.ExtractedFieldsJson = System.Text.Json.JsonSerializer.Serialize(_extractionResult);

                var resp = await Http.PutAsJsonAsync($"api/TicketInterventions/{Id}", _intervention);

                if (resp.IsSuccessStatusCode)
                {
                    await LoadIntervention();
                    Reset();
                }
                else
                {
                    _errorMessage = $"Errore salvataggio: {resp.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore: {ex.Message}";
            }
            finally
            {
                _isSaving = false;
                StateHasChanged();
            }
        }

        private async Task ClearReceiptData()
        {
            if (_intervention == null) return;

            _isSaving = true;
            StateHasChanged();

            try
            {
                _intervention.ExtractedTotalAmount = null;
                _intervention.ExtractedTaxAmount = null;
                _intervention.ExtractedTransactionDate = null;
                _intervention.ExtractedMerchantName = null;
                _intervention.ExtractedDescription = null;
                _intervention.ExtractedCurrency = null;
                _intervention.ExtractionConfidence = null;
                _intervention.ReceiptProcessedDate = null;
                _intervention.ExtractionConfirmed = false;
                _intervention.ExtractedFieldsJson = null;

                var resp = await Http.PutAsJsonAsync($"api/TicketInterventions/{Id}", _intervention);

                if (resp.IsSuccessStatusCode)
                {
                    await LoadIntervention();
                }
                else
                {
                    _errorMessage = $"Errore eliminazione: {resp.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore: {ex.Message}";
            }
            finally
            {
                _isSaving = false;
                StateHasChanged();
            }
        }

        private void Reset()
        {
            _extractionResult = null;
            _errorMessage = null;
            _isProcessing = false;
            _previewDataUrl = null;
            _previewContentType = null;
            _previewFileName = null;
            _editAmount = null;
            _editTax = null;
            _editDate = null;
            _editMerchant = null;
            _editDescription = null;
        }

        private string GetConfidencePercent()
        {
            if (_extractionResult?.AverageConfidence.HasValue == true)
                return $"{(_extractionResult.AverageConfidence.Value * 100):F1}%";
            return "N/A";
        }

        private string GetConfidenceColor(float? confidence)
        {
            if (!confidence.HasValue) return "secondary";
            return confidence.Value >= 0.8f ? "success" : confidence.Value >= 0.5f ? "warning" : "danger";
        }

        private bool IsImagePreview => _previewContentType != null &&
            _previewContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        private bool IsPdfPreview => _previewContentType != null &&
            _previewContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
    }
}
