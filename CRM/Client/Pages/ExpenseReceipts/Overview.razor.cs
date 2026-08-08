using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ExpenseReceipts
{
    /// <summary>
    /// Elenco trasversale delle note spese: la "zona accessibile" che prima non esisteva, perche'
    /// una nota spese si poteva vedere solo scendendo dentro un intervento.
    /// </summary>
    public partial class Overview : ComponentBase
    {
        [Inject] private HttpClient Http { get; set; }
        [Inject] private NavigationManager NavigationManager { get; set; }
        [Inject] private IHeaderService HeaderService { get; set; }
        [Inject] private IBaseRestService<ApplicationUser, UsersFilterModel, string> UsersService { get; set; }
        [Inject] private Microsoft.JSInterop.IJSRuntime JS { get; set; }
        [Inject] private Radzen.NotificationService NotificationService { get; set; }

        private PageHeaderModel _pageHeader;

        private ExpenseReceiptFilter _filter = new();
        private ExpenseReceiptSummaryDTO _summary = new();
        private List<ExpenseReceiptDTO> _items = new();
        private List<ApplicationUser> _users = new();

        private int _totalCount;
        private bool _canSeeAll;
        private bool _loading = true;

        /// <summary>
        /// Quando si guardano le spese di tutti ha senso vedere chi ha speso cosa; sul proprio
        /// mese quella colonna e' rumore.
        /// </summary>
        private bool _showUserColumn => string.IsNullOrEmpty(_filter.IdUserSpender);

        private string _period = "month";

        /// <summary>
        /// Periodo su cui aprire l'elenco ("/ExpenseReceipts?period=all"). Lo usa chi ha appena
        /// salvato una spesa vecchia: mandarlo sul mese corrente significherebbe mostrargli una
        /// pagina vuota subito dopo un salvataggio riuscito.
        /// </summary>
        [SupplyParameterFromQuery(Name = "period")]
        public string PeriodParam { get; set; }

        /// <summary>
        /// Spese di una singola iniziativa ("/ExpenseReceipts?idInitiative=12"): e' la nota spese
        /// della trasferta, aperta dal resoconto.
        /// </summary>
        [SupplyParameterFromQuery(Name = "idInitiative")]
        public int? InitiativeParam { get; set; }

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();

            // La pagina si apre gia' rispondendo alla domanda per cui esiste: quanto ho speso
            // io questo mese. Chi vuole altro cambia i filtri, ma il caso frequente non richiede
            // di impostare niente.
            //
            // Arrivando da un'iniziativa le regole cambiano entrambe: il periodo si apre a tutto e
            // il filtro sulla persona sparisce. Un viaggio di marzo aperto a giugno, o le spese dei
            // colleghi che erano in fiera, mostrerebbero altrimenti una pagina vuota - che si legge
            // come "non ci sono spese" e non come "le stai guardando dalla finestra sbagliata".
            var fromInitiative = InitiativeParam is > 0;
            ApplyPeriod(fromInitiative ? "all" : (string.IsNullOrWhiteSpace(PeriodParam) ? "month" : PeriodParam));
            _filter.PageSize = 25;

            await LoadCurrentUserAsync();

            if (fromInitiative)
            {
                _filter.IdInitiative = InitiativeParam;
                _filter.IdUserSpender = null;
            }

            await ReloadAsync();
        }

        /// <summary>Il filtro predefinito e' l'utente corrente: lo si ricava dal primo caricamento.</summary>
        private async Task LoadCurrentUserAsync()
        {
            try
            {
                var me = await Http.GetFromJsonAsync<ApplicationUser>("api/Users/CurrentUser");
                if (me != null)
                    _filter.IdUserSpender = me.Id;
            }
            catch (Exception ex)
            {
                // Senza l'utente corrente si parte da "tutti": il server restringe comunque la
                // visibilita' per chi non e' admin, quindi non si vede nulla di piu' del dovuto.
                Console.WriteLine($"Utente corrente non determinato, filtro su tutti: {ex.Message}");
            }
        }

        private void ApplyPeriod(string period)
        {
            _period = period;
            var today = DateTime.Today;

            switch (period)
            {
                // L'intervallo scelto a mano non ha date da calcolare: sono gia' nel filtro, e
                // ricalcolarle qui vorrebbe dire cancellare proprio quello che si e' scelto.
                case "custom":
                    break;
                case "month":
                    _filter.DateFrom = new DateTime(today.Year, today.Month, 1);
                    _filter.DateTo = _filter.DateFrom.Value.AddMonths(1).AddDays(-1);
                    break;
                case "prevmonth":
                    var first = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    _filter.DateFrom = first;
                    _filter.DateTo = first.AddMonths(1).AddDays(-1);
                    break;
                case "year":
                    _filter.DateFrom = new DateTime(today.Year, 1, 1);
                    _filter.DateTo = new DateTime(today.Year, 12, 31);
                    break;
                default:
                    _filter.DateFrom = null;
                    _filter.DateTo = null;
                    break;
            }
        }

        private async Task SetPeriod(string period)
        {
            ApplyPeriod(period);
            _filter.PageNumber = 1;
            await ReloadAsync();
        }

        /// <summary>
        /// Passa all'intervallo scelto a mano partendo da quello che si sta gia' guardando: chi
        /// arriva dal mese corrente vuole quasi sempre spostarne un estremo, non ricominciare da
        /// due caselle vuote. Da "Tutte" non ci sono date da cui partire e si propone l'ultimo
        /// mese, che e' il periodo con cui si lavora piu' spesso.
        /// </summary>
        private async Task StartCustomPeriod()
        {
            if (_period == "custom")
                return;

            var today = DateTime.Today;

            _filter.DateFrom ??= today.AddMonths(-1);
            _filter.DateTo ??= today;

            _period = "custom";
            _filter.PageNumber = 1;
            await ReloadAsync();
        }

        /// <summary>Valore per il campo data: vuoto quando la data non c'e'.</summary>
        private static string DateValue(DateTime? date) => date?.ToString("yyyy-MM-dd") ?? string.Empty;

        private async Task SetDateFrom(ChangeEventArgs args)
        {
            var date = ParseDate(args);

            // Svuotare l'inizio significa "da sempre fino a": e' un intervallo legittimo, non un
            // errore da rifiutare.
            _filter.DateFrom = date;

            // Un intervallo rovesciato non restituisce niente, e una pagina vuota si legge come
            // "non ci sono spese" invece che "hai chiesto un periodo impossibile": si sposta
            // l'altro estremo, che e' quello che l'operatore stava per fare comunque.
            if (_filter.DateFrom.HasValue && _filter.DateTo.HasValue && _filter.DateTo < _filter.DateFrom)
                _filter.DateTo = _filter.DateFrom;

            await ApplyCustomRangeAsync();
        }

        private async Task SetDateTo(ChangeEventArgs args)
        {
            _filter.DateTo = ParseDate(args);

            if (_filter.DateFrom.HasValue && _filter.DateTo.HasValue && _filter.DateFrom > _filter.DateTo)
                _filter.DateFrom = _filter.DateTo;

            await ApplyCustomRangeAsync();
        }

        private async Task ApplyCustomRangeAsync()
        {
            _period = "custom";
            _filter.PageNumber = 1;
            await ReloadAsync();
        }

        private static DateTime? ParseDate(ChangeEventArgs args)
        {
            var raw = args.Value?.ToString();

            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed.Date
                : null;
        }

        private async Task SetContext(ExpenseContextFilter context)
        {
            _filter.Context = context;
            _filter.PageNumber = 1;
            await ReloadAsync();
        }

        private async Task ToggleAllUsers()
        {
            _filter.IdUserSpender = string.IsNullOrEmpty(_filter.IdUserSpender)
                ? (await GetCurrentUserIdAsync())
                : null;

            _filter.PageNumber = 1;
            await ReloadAsync();
        }

        private string _currentUserId;

        private async Task<string> GetCurrentUserIdAsync()
        {
            if (!string.IsNullOrEmpty(_currentUserId))
                return _currentUserId;

            try
            {
                var me = await Http.GetFromJsonAsync<ApplicationUser>("api/Users/CurrentUser");
                _currentUserId = me?.Id;
            }
            catch { }

            return _currentUserId;
        }

        /// <summary>
        /// Quante note spese esistono, con gli stessi filtri ma senza limite di periodo.
        /// <para>
        /// Serve a un caso preciso: si registra uno scontrino di due mesi fa, si torna
        /// all'elenco - che si apre sul mese corrente - e non c'e'. Sembra che il salvataggio non
        /// abbia funzionato. La pagina vuota deve dire dove sono finite, non lasciar dedurre.
        /// </para>
        /// </summary>
        private async Task CountOutsidePeriodAsync()
        {
            // Domanda che ha senso solo quando il periodo taglia qualcosa e non si vede niente.
            if (_totalCount > 0 || _period == "all")
            {
                _countOutsidePeriod = 0;
                return;
            }

            try
            {
                var from = _filter.DateFrom;
                var to = _filter.DateTo;

                _filter.DateFrom = null;
                _filter.DateTo = null;

                var all = await Http.GetFromJsonAsync<SearchResponse>($"api/ExpenseReceipts?{BuildQuery()}");
                _countOutsidePeriod = all?.TotalCount ?? 0;

                _filter.DateFrom = from;
                _filter.DateTo = to;
            }
            catch (Exception ex)
            {
                // E' un aiuto, non un dato: se non arriva, l'elenco vuoto resta vuoto e basta.
                _countOutsidePeriod = 0;
                Console.WriteLine($"Conteggio fuori periodo non disponibile: {ex.Message}");
            }
        }

        private int _countOutsidePeriod;

        private async Task ReloadAsync()
        {
            _loading = true;
            StateHasChanged();

            try
            {
                var query = BuildQuery();

                var list = await Http.GetFromJsonAsync<SearchResponse>($"api/ExpenseReceipts?{query}");
                if (list != null)
                {
                    _items = list.Items ?? new List<ExpenseReceiptDTO>();
                    _totalCount = list.TotalCount;
                    _canSeeAll = list.CanSeeAll;
                }

                _summary = await Http.GetFromJsonAsync<ExpenseReceiptSummaryDTO>($"api/ExpenseReceipts/summary?{query}")
                           ?? new ExpenseReceiptSummaryDTO();

                await CountOutsidePeriodAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento note spese: {ex.Message}");
                _items = new List<ExpenseReceiptDTO>();
                _summary = new ExpenseReceiptSummaryDTO();
                _totalCount = 0;
            }
            finally
            {
                _loading = false;
                StateHasChanged();
            }
        }

        private bool _isDownloading;

        /// <summary>
        /// Prospetto PDF dell'insieme filtrato, raggruppato per tipologia.
        /// <para>
        /// Riusa <see cref="BuildQuery"/>: quello che si stampa e' esattamente quello che si sta
        /// guardando. Costruire una seconda query per la stampa e' il modo piu' rapido per
        /// ritrovarsi un totale a schermo e uno diverso sulla carta.
        /// </para>
        /// </summary>
        private async Task DownloadReport()
        {
            try
            {
                _isDownloading = true;

                var response = await Http.GetAsync($"api/ExpenseReceipts/report?{BuildQuery()}");

                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    NotificationService.Notify(Radzen.NotificationSeverity.Info, "Report note spese",
                        "Non c'è nessuna nota spese da stampare con questi filtri.");
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    NotificationService.Notify(Radzen.NotificationSeverity.Error, "Report note spese",
                        $"Prospetto non generato ({(int)response.StatusCode}).");
                    return;
                }

                var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                               ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                               ?? "note-spese.pdf";

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await JS.InvokeVoidAsync("downloadFileFromBytes", fileName, "application/pdf", bytes);
            }
            catch (Exception ex)
            {
                NotificationService.Notify(Radzen.NotificationSeverity.Error, "Report note spese", ex.Message);
                Console.Error.WriteLine($"Errore report note spese: {ex.Message}");
            }
            finally
            {
                _isDownloading = false;
            }
        }

        private string BuildQuery()
        {
            var parts = new List<string>();

            if (_filter.DateFrom.HasValue) parts.Add($"dateFrom={_filter.DateFrom.Value:yyyy-MM-dd}");
            if (_filter.DateTo.HasValue) parts.Add($"dateTo={_filter.DateTo.Value:yyyy-MM-dd}");
            if (!string.IsNullOrEmpty(_filter.IdUserSpender)) parts.Add($"idUserSpender={Uri.EscapeDataString(_filter.IdUserSpender)}");
            if (_filter.Context != ExpenseContextFilter.All) parts.Add($"context={(int)_filter.Context}");
            if (_filter.Category.HasValue) parts.Add($"category={(int)_filter.Category.Value}");
            if (_filter.MissingCategory == true) parts.Add("missingCategory=true");
            if (_filter.NeedsConversion == true) parts.Add("needsConversion=true");
            if (!string.IsNullOrWhiteSpace(_filter.Search)) parts.Add($"search={Uri.EscapeDataString(_filter.Search)}");

            parts.Add($"pageNumber={_filter.PageNumber}");
            parts.Add($"pageSize={_filter.PageSize}");

            return string.Join("&", parts);
        }

        private async Task OnCategoryFilterChanged(ChangeEventArgs args)
        {
            var raw = args.Value?.ToString();

            _filter.Category = string.IsNullOrWhiteSpace(raw)
                ? null
                : Enum.TryParse<ExpenseCategory>(raw, out var parsed) ? parsed : null;

            // Chiedere una tipologia e insieme "quelle senza" darebbe sempre zero righe.
            if (_filter.Category != null)
                _filter.MissingCategory = null;

            _filter.PageNumber = 1;
            await ReloadAsync();
        }

        private async Task ToggleMissingCategory()
        {
            _filter.MissingCategory = _filter.MissingCategory == true ? null : true;
            _filter.PageNumber = 1;
            await ReloadAsync();
        }

        private async Task ShowOnlyToConvert()
        {
            _filter.NeedsConversion = _filter.NeedsConversion == true ? null : true;
            _filter.PageNumber = 1;
            await ReloadAsync();
        }

        private async Task GoToPage(int page)
        {
            if (page < 1 || page > TotalPages) return;
            _filter.PageNumber = page;
            await ReloadAsync();
        }

        private int TotalPages => _filter.PageSize > 0
            ? (int)Math.Ceiling(_totalCount / (double)_filter.PageSize)
            : 1;

        private string FormatMoney(decimal? amount, string currency) => CurrencyUi.Money(amount, currency);

        /// <summary>Etichetta del contesto: intervento, attivita' o costo generale.</summary>
        private (string Icon, string Text, string Url) DescribeContext(ExpenseReceiptDTO receipt)
        {
            if (receipt.TicketInterventionId.HasValue)
                return ("build", $"Intervento #{receipt.TicketInterventionId}",
                        $"/TicketInterventions/{receipt.TicketInterventionId}");

            if (receipt.IdActivity.HasValue)
                return ("event_available", receipt.ActivitySubject ?? $"Attivita #{receipt.IdActivity}",
                        $"/Activities/{receipt.IdActivity}/Info");

            // La spesa di una fiera senza visita risultava "costo generale" proprio mentre il
            // consuntivo della fiera la contava fra i suoi costi: due pagine che dicevano il
            // contrario sullo stesso scontrino.
            if (receipt.IdInitiative.HasValue)
                return ("event_note", receipt.InitiativeName ?? $"Iniziativa #{receipt.IdInitiative}",
                        $"/Initiatives/{receipt.IdInitiative}/Info?view=notespese");

            return ("receipt_long", "Costo generale", null);
        }

        /// <summary>
        /// Apre sempre il dettaglio. Prima si navigava solo con l'intervento: sulle spese senza
        /// contesto il click sulla riga non faceva assolutamente nulla, e quelle note spese
        /// risultavano visibili in elenco ma inaccessibili.
        /// </summary>
        private void OpenReceipt(ExpenseReceiptDTO receipt)
        {
            NavigationManager.NavigateTo(receipt.TicketInterventionId.HasValue
                ? $"/TicketInterventions/{receipt.TicketInterventionId}/ExpenseReceipts/{receipt.Id}/Details"
                : $"/ExpenseReceipts/{receipt.Id}/Details");
        }

        private class SearchResponse
        {
            public List<ExpenseReceiptDTO> Items { get; set; }
            public int TotalCount { get; set; }
            public bool CanSeeAll { get; set; }
        }
    }
}
