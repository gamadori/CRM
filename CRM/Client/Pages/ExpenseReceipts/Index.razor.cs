using CRM.Client.Helpers;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ExpenseReceipts
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        /// <summary>
        /// Contenitore delle spese: uno dei due e' valorizzato, mai entrambi.
        /// <para>
        /// La pagina serve l'intervento e l'attivita' perche' l'elenco, il riepilogo e le azioni
        /// sono gli stessi; cambia solo da dove arrivano i dati e dove portano i collegamenti.
        /// </para>
        /// </summary>
        [Parameter] public int? InterventionId { get; set; }

        [Parameter] public int? ActivityId { get; set; }

        /// <summary>
        /// True quando l'elenco e' dentro un'altra pagina (la scheda dell'attivita'): niente
        /// intestazione propria e niente pulsante di ritorno, li mette chi contiene.
        /// </summary>
        [Parameter] public bool Embedded { get; set; }

        private List<ExpenseReceiptDTO> _receipts;
        private ExpenseReceiptSummaryDTO _summary;
        private bool _isLoading = true;
        private bool _isConfirming;
        private bool _isDeleting;
        private string _errorMessage;

        /// <summary>Radice delle rotte figlie: e' il contenitore da cui si e' arrivati.</summary>
        private string Root => ActivityId.HasValue
            ? $"/Activities/{ActivityId}"
            : $"/TicketInterventions/{InterventionId}";

        private string CreateUrl => $"{Root}/ExpenseReceipts/Create";

        private string DetailsUrl(int receiptId) => $"{Root}/ExpenseReceipts/{receiptId}/Details";

        private string EditUrl(int receiptId) => $"{Root}/ExpenseReceipts/{receiptId}/Edit";

        private string BackUrl => ActivityId.HasValue ? $"/Activities/{ActivityId}" : Root;

        private string BackText => ActivityId.HasValue ? "Torna all'attività" : "Torna all'intervento";

        private string EmptyText => ActivityId.HasValue
            ? "Nessuna nota spese presente per questa attività."
            : "Nessuna nota spese presente per questo intervento.";

        /// <summary>Importo in valuta base: i totali del riepilogo sono gia' convertiti.</summary>
        private string Base(decimal amount) => CurrencyUi.Money(amount, _summary?.BaseCurrency);

        protected override async Task OnParametersSetAsync()
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                _isLoading = true;

                var url = ActivityId.HasValue
                    ? $"api/ExpenseReceipts/activity/{ActivityId}"
                    : $"api/ExpenseReceipts/intervention/{InterventionId}";

                var response = await Http.GetFromJsonAsync<ExpenseReceiptsResponse>(url);

                if (response != null)
                {
                    _receipts = response.Receipts;
                    _summary = response.Summary;
                }
            }
            catch (Exception ex)
            {
                _errorMessage = "Errore nel caricamento delle note spese.";
                Console.Error.WriteLine($"Errore caricamento note spese: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task ConfirmReceipt(int receiptId)
        {
            try
            {
                _isConfirming = true;

                var response = await Http.PostAsync($"api/ExpenseReceipts/{receiptId}/confirm", null);

                if (response.IsSuccessStatusCode)
                    await LoadData();
                else
                    _errorMessage = "Conferma non riuscita.";
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore: {ex.Message}";
                Console.Error.WriteLine($"Errore conferma nota spese: {ex.Message}");
            }
            finally
            {
                _isConfirming = false;
            }
        }

        /// <summary>
        /// Cancellazione con conferma. Il riepilogo si ricarica dal server invece di essere
        /// aggiustato qui: i totali dipendono dalla conversione, non dal numero di righe.
        /// </summary>
        private async Task DeleteReceipt(ExpenseReceiptDTO receipt)
        {
            var what = string.IsNullOrWhiteSpace(receipt.MerchantName)
                ? $"la nota spese #{receipt.Id}"
                : $"la nota spese #{receipt.Id} ({receipt.MerchantName})";

            var confirmed = await DialogService.Confirm(
                $"Eliminare {what}? L'operazione non è reversibile.",
                "Elimina nota spese",
                new Radzen.ConfirmOptions { OkButtonText = "Elimina", CancelButtonText = "Annulla" });

            if (confirmed != true)
                return;

            try
            {
                _isDeleting = true;

                var response = await Http.DeleteAsync($"api/ExpenseReceipts/{receipt.Id}");

                if (response.IsSuccessStatusCode)
                {
                    await LoadData();
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _errorMessage = string.IsNullOrWhiteSpace(body)
                        ? $"Eliminazione non riuscita ({(int)response.StatusCode})."
                        : body;
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore: {ex.Message}";
                Console.Error.WriteLine($"Errore eliminazione nota spese: {ex.Message}");
            }
            finally
            {
                _isDeleting = false;
            }
        }

        private class ExpenseReceiptsResponse
        {
            public List<ExpenseReceiptDTO> Receipts { get; set; }
            public ExpenseReceiptSummaryDTO Summary { get; set; }
        }
    }
}
