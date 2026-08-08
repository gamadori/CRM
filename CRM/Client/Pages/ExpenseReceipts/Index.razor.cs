using CRM.Client.Helpers;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Iniziativa (fiera, trasferta) di cui si stanno guardando le spese. E' il TERZO
        /// contenitore possibile: a differenza degli altri due non e' il lavoro di una persona ma
        /// un'occasione condivisa, quindi le righe possono essere di piu' persone.
        /// </summary>
        [Parameter] public int? InitiativeId { get; set; }

        /// <summary>
        /// Falso quando chi guarda vede solo le proprie spese. Va detto, perche' su un'iniziativa
        /// il totale del resoconto comprende anche quelle dei colleghi e la differenza con la
        /// somma delle righe visibili si legge come un errore dei conti.
        /// </summary>
        private bool _canSeeAll = true;

        /// <summary>
        /// True quando l'elenco e' dentro un'altra pagina (la scheda dell'attivita'): niente
        /// intestazione propria e niente pulsante di ritorno, li mette chi contiene.
        /// </summary>
        [Parameter] public bool Embedded { get; set; }

        [Inject] private IJSRuntime JS { get; set; }

        private List<ExpenseReceiptDTO> _receipts;
        private ExpenseReceiptSummaryDTO _summary;
        private bool _isLoading = true;
        private bool _isConfirming;
        private bool _isDeleting;
        private string _errorMessage;

        /// <summary>
        /// Radice delle rotte figlie: e' il contenitore da cui si e' arrivati, e resta nel
        /// percorso fino al dettaglio della singola spesa. E' quello che fa tornare il breadcrumb
        /// alla fiera invece che all'elenco generale.
        /// </summary>
        private string Root => ActivityId.HasValue
            ? $"/Activities/{ActivityId}"
            : InitiativeId.HasValue
                ? $"/Initiatives/{InitiativeId}"
                : $"/TicketInterventions/{InterventionId}";

        private string CreateUrl => $"{Root}/ExpenseReceipts/Create";

        private string DetailsUrl(int receiptId) => $"{Root}/ExpenseReceipts/{receiptId}/Details";

        private string EditUrl(int receiptId) => $"{Root}/ExpenseReceipts/{receiptId}/Edit";

        private string BackUrl => ActivityId.HasValue
            ? $"/Activities/{ActivityId}"
            : InitiativeId.HasValue
                ? $"/Initiatives/{InitiativeId}/Info"
                : Root;

        private string BackText => ActivityId.HasValue
            ? "Torna all'attività"
            : InitiativeId.HasValue
                ? "Torna all'iniziativa"
                : "Torna all'intervento";

        private string EmptyText => InitiativeId.HasValue
            ? "Nessuna nota spese registrata per questa iniziativa."
            : ActivityId.HasValue
                ? "Nessuna nota spese presente per questa attività."
                : "Nessuna nota spese presente per questo intervento.";

        /// <summary>Importo in valuta base: i totali del riepilogo sono gia' convertiti.</summary>
        private string Base(decimal amount) => CurrencyUi.Money(amount, _summary?.BaseCurrency);

        /// <summary>
        /// La colonna "Persona" compare solo dove aggiunge qualcosa: su un'iniziativa, dove le
        /// spese sono di piu' colleghi, o quando comunque ce n'e' piu' d'una. Su un intervento con
        /// un tecnico solo sarebbe la stessa parola ripetuta a ogni riga.
        /// </summary>
        private bool _showUserColumn =>
            InitiativeId.HasValue
            || (_receipts?.Select(r => r.IdUserSpender).Distinct().Count() > 1);

        /// <summary>
        /// La riga intera apre il dettaglio, come nell'elenco generale: con le icone tutte uguali
        /// in fondo, il bersaglio da centrare per guardare una spesa era largo sedici pixel.
        /// </summary>
        private void OpenReceipt(int receiptId)
            => NavigationManager.NavigateTo(DetailsUrl(receiptId));

        private bool _isDownloading;

        /// <summary>
        /// Il contenitore tradotto in filtro: il prospetto passa dallo stesso endpoint dell'elenco
        /// generale, quindi vede le stesse spese con lo stesso vincolo di visibilita'.
        /// </summary>
        private string ReportQuery() =>
            InitiativeId.HasValue ? $"idInitiative={InitiativeId}"
            : ActivityId.HasValue ? $"idActivity={ActivityId}"
            : $"ticketInterventionId={InterventionId}";

        private async Task DownloadReport()
        {
            try
            {
                _isDownloading = true;
                _errorMessage = null;

                var response = await Http.GetAsync($"api/ExpenseReceipts/report?{ReportQuery()}");

                // Nessuna riga, nessun documento: un PDF con la sola intestazione somiglia troppo
                // a un errore di stampa.
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    _errorMessage = "Non c'è nessuna nota spese da stampare.";
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _errorMessage = $"Prospetto non generato ({(int)response.StatusCode}).";
                    return;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await JS.InvokeVoidAsync("downloadFileFromBytes", FileNameOf(response), "application/pdf", bytes);
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore: {ex.Message}";
                Console.Error.WriteLine($"Errore report note spese: {ex.Message}");
            }
            finally
            {
                _isDownloading = false;
            }
        }

        /// <summary>Nome file deciso dal server: qui si legge soltanto, senza reinventarlo.</summary>
        private static string FileNameOf(HttpResponseMessage response) =>
            response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? "note-spese.pdf";

        protected override async Task OnParametersSetAsync()
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                _isLoading = true;

                var url = InitiativeId.HasValue
                    ? $"api/ExpenseReceipts/initiative/{InitiativeId}"
                    : ActivityId.HasValue
                        ? $"api/ExpenseReceipts/activity/{ActivityId}"
                        : $"api/ExpenseReceipts/intervention/{InterventionId}";

                var response = await Http.GetFromJsonAsync<ExpenseReceiptsResponse>(url);

                if (response != null)
                {
                    _receipts = response.Receipts;
                    _summary = response.Summary;

                    // Solo la rotta dell'iniziativa lo dichiara: sugli altri due contenitori le
                    // spese sono del lavoro che si ha davanti e la domanda non si pone.
                    _canSeeAll = !InitiativeId.HasValue || response.CanSeeAll;
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

            /// <summary>Valorizzato dalla sola rotta dell'iniziativa; altrove resta al default.</summary>
            public bool CanSeeAll { get; set; } = true;
        }
    }
}
