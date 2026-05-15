using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.IO;
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
        [Parameter] public int InterventionId { get; set; }

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

        protected override void OnInitialized()
        {
            _model.TicketInterventionId = InterventionId;
            _model.Currency = "EUR";
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

                var uploadResponse = await Http.PostAsync($"api/AttachmentFiles/{InterventionId}/upload", content);
                
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
                    _model.Currency = _extractionResult.Currency ?? "EUR";
                    _model.Description = $"{_extractionResult.MerchantName} - {_extractionResult.TransactionDate?.ToString("dd/MM/yyyy")}";
                    _model.ExtractionConfidence = _extractionResult.AverageConfidence;
                    _model.ExtractedFieldsJson = JsonSerializer.Serialize(_extractionResult);
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
                _isSaving = true;

                var response = await Http.PostAsJsonAsync("api/ExpenseReceipts", _model);

                if (response.IsSuccessStatusCode)
                {
                    var created = await response.Content.ReadFromJsonAsync<ExpenseReceiptDTO>();
                    NavigationManager.NavigateTo($"/TicketInterventions/{InterventionId}/ExpenseReceipts/{created.Id}/Details");
                }
                else
                {
                    _errorMessage = "Errore durante il salvataggio della nota spese.";
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
            _model = new ExpenseReceiptCreateUpdateDTO
            {
                TicketInterventionId = InterventionId,
                Currency = "EUR"
            };
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

        private class AttachmentFileUploadResult
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
