using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ExpenseReceipts
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
        /// <summary>Null quando la spesa non appartiene a un intervento (costo generale, visita).</summary>
        [Parameter] public int? InterventionId { get; set; }

        /// <summary>Valorizzato quando si gestisce la spesa dalla scheda dell'attivita'.</summary>
        [Parameter] public int? ActivityId { get; set; }

        /// <summary>
        /// Radice da cui si e' arrivati. Tenerla in un punto solo e' quello che evita gli
        /// indirizzi con il doppio slash e le pagine da cui non si torna indietro.
        /// </summary>
        private string Root => ActivityId.HasValue
            ? $"/Activities/{ActivityId}"
            : InterventionId.HasValue
                ? $"/TicketInterventions/{InterventionId}"
                : null;

        private string ListUrl => ActivityId.HasValue
            ? $"/Activities/{ActivityId}/Info?view=notespese"
            : Root != null
                ? $"{Root}/ExpenseReceipts"
                : "/ExpenseReceipts";

        private string DetailsUrl => Root != null
            ? $"{Root}/ExpenseReceipts/{ReceiptId}/Details"
            : $"/ExpenseReceipts/{ReceiptId}/Details";
        [Parameter] public int ReceiptId { get; set; }

        private ExpenseReceiptCreateUpdateDTO _model = new();
        private List<ExpenseReceiptDocumentDTO> _documentResults = new();

        /// <summary>Oggetto della visita: arriva con la nota spese, senza una seconda chiamata.</summary>
        private string _activitySubject;

        private string ActivityLabel => string.IsNullOrWhiteSpace(_activitySubject)
            ? $"Attività #{ActivityId}"
            : _activitySubject;

        private bool _isLoading = true;
        private bool _isSaving = false;
        private bool _notFound = false;
        private string _errorMessage;

        protected override async Task OnInitializedAsync()
        {
            await LoadReceipt();
        }

        private async Task LoadReceipt()
        {
            try
            {
                _isLoading = true;
                var receipt = await Http.GetFromJsonAsync<ExpenseReceiptDTO>($"api/ExpenseReceipts/{ReceiptId}");

                if (receipt == null)
                {
                    _notFound = true;
                    return;
                }

                _activitySubject = receipt.ActivitySubject;

                _model = new ExpenseReceiptCreateUpdateDTO
                {
                    Id = receipt.Id,
                    TicketInterventionId = receipt.TicketInterventionId,
                    // Contesto, chi ha speso e cambio vanno rispediti come sono: il server
                    // sovrascrive con quello che riceve, quindi ometterli qui significava
                    // staccare l'attivita' collegata a ogni salvataggio, in silenzio.
                    IdActivity = receipt.IdActivity,
                    IdUserSpender = receipt.IdUserSpender,
                    ExchangeRate = receipt.ExchangeRate,
                    Description = receipt.Description,
                    AttachmentFileId = receipt.AttachmentFileId,
                    TotalAmount = receipt.TotalAmount,
                    TaxAmount = receipt.TaxAmount,
                    TransactionDate = receipt.TransactionDate,
                    MerchantName = receipt.MerchantName,
                    Currency = receipt.Currency,
                    Notes = receipt.Notes,
                    IsConfirmed = receipt.IsConfirmed,
                    ExtractionConfidence = receipt.ExtractionConfidence,
                    ExtractedFieldsJson = receipt.ExtractedFieldsJson,
                    Documents = receipt.Documents ?? new()
                };
                _documentResults = _model.Documents;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore caricamento nota spese: {ex.Message}");
                _notFound = true;
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task SaveReceipt()
        {
            try
            {
                _isSaving = true;
                _errorMessage = null;

                UpdateMultiDocumentTotals();

                // L'indirizzo aveva "/Details" in coda, ma il controller espone [HttpPut("{id}")]:
                // ogni salvataggio finiva in 404 e usciva come "Errore durante il salvataggio".
                var response = await Http.PutAsJsonAsync($"api/ExpenseReceipts/{ReceiptId}", _model);

                if (response.IsSuccessStatusCode)
                {
                    NavigationManager.NavigateTo(DetailsUrl);
                }
                else
                {
                    // Il messaggio del server dice cosa non va; quello generico nascondeva un 404.
                    var body = await response.Content.ReadAsStringAsync();
                    _errorMessage = string.IsNullOrWhiteSpace(body)
                        ? $"Salvataggio non riuscito ({(int)response.StatusCode})."
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

        private void AddLine(ExpenseReceiptDocumentDTO document)
        {
            document.Lines ??= new();
            document.Lines.Add(new ExpenseReceiptDocumentLineDTO
            {
                SortOrder = document.Lines.Count,
                Quantity = 1
            });
        }

        private static void RemoveLine(ExpenseReceiptDocumentDTO document, ExpenseReceiptDocumentLineDTO line)
            => document.Lines?.Remove(line);

        private void UpdateMultiDocumentTotals()
        {
            if (_documentResults.Count == 0)
                return;

            for (var documentIndex = 0; documentIndex < _documentResults.Count; documentIndex++)
            {
                var document = _documentResults[documentIndex];
                document.SortOrder = documentIndex;
                document.Lines ??= new();
                for (var lineIndex = 0; lineIndex < document.Lines.Count; lineIndex++)
                    document.Lines[lineIndex].SortOrder = lineIndex;
            }

            _model.Documents = _documentResults;
            _model.TotalAmount = SumNullable(_documentResults.Select(document => document.TotalAmount));
            _model.TaxAmount = SumNullable(_documentResults.Select(document => document.TaxAmount));
            _model.TransactionDate = _documentResults
                .Where(document => document.TransactionDate.HasValue)
                .Select(document => document.TransactionDate)
                .Min();

            var currencies = _documentResults
                .Select(document => document.Currency?.Trim().ToUpperInvariant())
                .Where(currency => !string.IsNullOrWhiteSpace(currency))
                .Distinct()
                .ToList();
            _model.Currency = currencies.Count == 1 ? currencies[0] : null;
            _model.MerchantName = _documentResults.Count == 1
                ? _documentResults[0].MerchantName
                : $"{_documentResults.Count} documenti fiscali";
        }

        private static decimal? SumNullable(IEnumerable<decimal?> values)
        {
            var presentValues = values.Where(value => value.HasValue).Select(value => value.Value).ToList();
            return presentValues.Count == 0 ? null : presentValues.Sum();
        }
    }
}
