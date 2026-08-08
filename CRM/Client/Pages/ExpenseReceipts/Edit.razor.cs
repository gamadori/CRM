using CRM.Shared;
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

        /// <summary>Valorizzato quando si arriva dalla cartella spese di un'iniziativa.</summary>
        [Parameter] public int? InitiativeId { get; set; }

        /// <summary>
        /// Radice da cui si e' arrivati. Tenerla in un punto solo e' quello che evita gli
        /// indirizzi con il doppio slash e le pagine da cui non si torna indietro.
        /// </summary>
        private string Root => ActivityId.HasValue
            ? $"/Activities/{ActivityId}"
            : InitiativeId.HasValue
                ? $"/Initiatives/{InitiativeId}"
                : InterventionId.HasValue
                    ? $"/TicketInterventions/{InterventionId}"
                    : null;

        private string ListUrl => ActivityId.HasValue
            ? $"/Activities/{ActivityId}/Info?view=notespese"
            : InitiativeId.HasValue
                ? $"/Initiatives/{InitiativeId}/Info?view=notespese"
                : Root != null
                    ? $"{Root}/ExpenseReceipts"
                    : "/ExpenseReceipts";

        private string DetailsUrl => Root != null
            ? $"{Root}/ExpenseReceipts/{ReceiptId}/Details"
            : $"/ExpenseReceipts/{ReceiptId}/Details";
        [Parameter] public int ReceiptId { get; set; }

        private ExpenseReceiptCreateUpdateDTO _model = new();
        private List<ExpenseReceiptDocumentDTO> _documentResults = new();

        /// <summary>
        /// Iniziative selezionabili. Qui l'elenco NON e' ristretto all'ultimo anno come in
        /// creazione: la modifica e' proprio il posto in cui si rimedia, riattaccando una spesa a
        /// una fiera vecchia che in creazione non compariva.
        /// </summary>
        private List<InitiativeDTO> _initiatives = new();

        /// <summary>Oggetto della visita: arriva con la nota spese, senza una seconda chiamata.</summary>
        private string _activitySubject;

        private string ActivityLabel => string.IsNullOrWhiteSpace(_activitySubject)
            ? $"Attività #{ActivityId}"
            : _activitySubject;

        /// <summary>Nome dell'iniziativa: arriva con la nota spese, senza una seconda chiamata.</summary>
        private string _initiativeName;

        private string InitiativeLabel => string.IsNullOrWhiteSpace(_initiativeName)
            ? $"Iniziativa #{InitiativeId}"
            : _initiativeName;

        private bool _isLoading = true;
        private bool _isSaving = false;
        private bool _notFound = false;
        private string _errorMessage;

        protected override async Task OnInitializedAsync()
        {
            await LoadReceipt();
            await LoadInitiativesAsync();
        }

        private async Task LoadInitiativesAsync()
        {
            try
            {
                _initiatives = await InitiativeService.GetListAsync(new InitiativeFilter()) ?? new List<InitiativeDTO>();
            }
            catch (Exception ex)
            {
                // Senza elenco il campo resta com'e': si perde la possibilita' di cambiarlo, non
                // il valore gia' salvato, che viaggia comunque nel modello.
                Console.WriteLine($"Iniziative non caricate: {ex.Message}");
            }
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
                _initiativeName = receipt.InitiativeName;

                _model = new ExpenseReceiptCreateUpdateDTO
                {
                    Id = receipt.Id,
                    TicketInterventionId = receipt.TicketInterventionId,
                    // Contesto, chi ha speso e cambio vanno rispediti come sono: il server
                    // sovrascrive con quello che riceve, quindi ometterli qui significava
                    // staccare l'attivita' collegata a ogni salvataggio, in silenzio.
                    IdActivity = receipt.IdActivity,
                    IdInitiative = receipt.IdInitiative,
                    IdUserSpender = receipt.IdUserSpender,
                    ExchangeRate = receipt.ExchangeRate,
                    Description = receipt.Description,
                    AttachmentFileId = receipt.AttachmentFileId,
                    TotalAmount = receipt.TotalAmount,
                    TaxAmount = receipt.TaxAmount,
                    TransactionDate = receipt.TransactionDate,
                    MerchantName = receipt.MerchantName,
                    Currency = receipt.Currency,

                    // Tipologia e provenienza tornano indietro come sono: il server distingue una
                    // conferma da una correzione confrontandole, e ometterle qui trasformerebbe
                    // ogni salvataggio in una scelta manuale, falsando la misura dell'automatismo.
                    Category = receipt.Category,
                    CategorySuggested = receipt.CategorySuggested,
                    CategorySource = receipt.CategorySource,
                    CategoryConfidence = receipt.CategoryConfidence,
                    CategoryReason = receipt.CategoryReason,

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

        /// <summary>
        /// Tipologia proposta per un documento: e' quella che c'e' finche' nessuno la tocca.
        /// Toccata, la provenienza diventa manuale e non c'e' piu' nessuna proposta da mostrare.
        /// </summary>
        private static ExpenseCategory? DocumentSuggestion(ExpenseReceiptDocumentDTO document) =>
            document.CategorySource == ExpenseCategorySource.Manual ? null : document.Category;

        private void OnCategoryChanged(ExpenseCategory? category)
        {
            _model.Category = category;
            _model.CategorySource = category.HasValue ? ExpenseCategorySource.Manual : null;
            _model.CategoryConfidence = null;
            _model.CategoryReason = null;
        }

        private static void OnDocumentCategoryChanged(ExpenseReceiptDocumentDTO document, ExpenseCategory? category)
        {
            document.Category = category;
            document.CategorySource = category.HasValue ? ExpenseCategorySource.Manual : null;
            document.CategoryConfidence = null;
            document.CategoryReason = null;
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

            // La tipologia della testata la fanno i documenti, che sono quelli che si modificano
            // qui. Con documenti che dicono cose diverse la testata non ne ha una: dichiararne
            // una sola direbbe il falso sugli altri.
            var categories = _documentResults.Select(document => document.Category).Distinct().ToList();
            if (categories.Count == 1)
            {
                var document = _documentResults[0];
                _model.Category = document.Category;
                _model.CategorySource = document.CategorySource;
                _model.CategoryConfidence = document.CategoryConfidence;
                _model.CategoryReason = document.CategoryReason;
            }
            else
            {
                _model.Category = null;
                _model.CategorySource = null;
                _model.CategoryConfidence = null;
                _model.CategoryReason = null;
            }
        }

        private static decimal? SumNullable(IEnumerable<decimal?> values)
        {
            var presentValues = values.Where(value => value.HasValue).Select(value => value.Value).ToList();
            return presentValues.Count == 0 ? null : presentValues.Sum();
        }
    }
}
