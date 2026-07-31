using CRM.Client.Helpers;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ExpenseReceipts
{
    [Authorize]
    public partial class Details : ComponentBase
    {
        /// <summary>Null quando la spesa non appartiene a un intervento (costo generale, visita).</summary>
        [Parameter] public int? InterventionId { get; set; }

        /// <summary>
        /// Indirizzi calcolati in un punto solo: con l'intervento si resta dentro il suo percorso,
        /// senza si torna all'elenco globale. Sparpagliare la condizione su ogni link e' il modo in
        /// cui erano nati "/TicketInterventions//ExpenseReceipts" e le pagine irraggiungibili.
        /// </summary>
        /// <summary>Valorizzato quando si gestisce la spesa dalla scheda dell'attivita'.</summary>
        [Parameter] public int? ActivityId { get; set; }

        private string Root => ActivityId.HasValue
            ? $"/Activities/{ActivityId}"
            : InterventionId.HasValue
                ? $"/TicketInterventions/{InterventionId}"
                : null;

        // Dall'attivita' si torna alla sua scheda gia' aperta sulle note spese: e' il posto da
        // cui si e' partiti, non un elenco a se' stante.
        private string ListUrl => ActivityId.HasValue
            ? $"/Activities/{ActivityId}/Info?view=notespese"
            : Root != null
                ? $"{Root}/ExpenseReceipts"
                : "/ExpenseReceipts";

        private string EditUrl => Root != null
            ? $"{Root}/ExpenseReceipts/{ReceiptId}/Edit"
            : $"/ExpenseReceipts/{ReceiptId}/Edit";

        /// <summary>Importo nella valuta della spesa: la pagina mostrava un euro fisso.</summary>
        private string Money(decimal? amount) => CurrencyUi.Money(amount, _receipt?.Currency);

        /// <summary>
        /// Contesto della spesa - intervento, visita o niente. Le tre voci stanno insieme perche'
        /// sono un'alternativa sola: icona, testo e destinazione cambiano tutti nello stesso punto.
        /// </summary>
        private string ContextIcon =>
            _receipt?.TicketInterventionId != null ? "build"
            : _receipt?.IdActivity != null ? "event_available"
            : "receipt_long";

        private string ContextText =>
            _receipt?.TicketInterventionId != null ? $"Intervento #{_receipt.TicketInterventionId}"
            : _receipt?.IdActivity != null ? (_receipt.ActivitySubject ?? $"Attivita #{_receipt.IdActivity}")
            : "Costo generale";

        /// <summary>Null sul costo generale: non c'e' niente da aprire, e un link finto e' peggio.</summary>
        private string ContextUrl =>
            _receipt?.TicketInterventionId != null ? $"/TicketInterventions/{_receipt.TicketInterventionId}"
            : _receipt?.IdActivity != null ? $"/Activities/{_receipt.IdActivity}/Info"
            : null;
        [Parameter] public int ReceiptId { get; set; }

        private ExpenseReceiptDTO _receipt;
        private List<ExpenseReceiptDocumentDTO> _documentResults = new();
        private bool _isLoading = true;
        private bool _isConfirming = false;
        private bool _isDeleting = false;

        protected override async Task OnInitializedAsync()
        {
            await LoadReceipt();
        }

        private async Task LoadReceipt()
        {
            try
            {
                _isLoading = true;
                _receipt = await Http.GetFromJsonAsync<ExpenseReceiptDTO>(
                    $"api/ExpenseReceipts/{ReceiptId}");
                _documentResults = _receipt?.Documents ?? new();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore caricamento nota spese: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private static string DocumentMoney(ExpenseReceiptDocumentDTO document, decimal? amount)
            => CurrencyUi.Money(amount, document.Currency);

        private async Task ConfirmReceipt()
        {
            try
            {
                _isConfirming = true;

                var response = await Http.PostAsync($"api/ExpenseReceipts/{ReceiptId}/confirm", null);

                if (response.IsSuccessStatusCode)
                {
                    await LoadReceipt();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore conferma nota spese: {ex.Message}");
            }
            finally
            {
                _isConfirming = false;
            }
        }

        private async Task DeleteReceipt()
        {
            if (!await JSRuntime.InvokeAsync<bool>("confirm", "Sei sicuro di voler eliminare questa nota spese?"))
                return;

            try
            {
                _isDeleting = true;

                var response = await Http.DeleteAsync($"api/ExpenseReceipts/{ReceiptId}");

                if (response.IsSuccessStatusCode)
                {
                    NavigationManager.NavigateTo(ListUrl);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore eliminazione nota spese: {ex.Message}");
            }
            finally
            {
                _isDeleting = false;
            }
        }

        private string GetConfidenceClass(float confidence)
        {
            if (confidence >= 0.9f) return "bg-success";
            if (confidence >= 0.7f) return "bg-warning";
            return "bg-danger";
        }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }
    }
}
