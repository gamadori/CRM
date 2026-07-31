using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
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

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();

            // La pagina si apre gia' rispondendo alla domanda per cui esiste: quanto ho speso
            // io questo mese. Chi vuole altro cambia i filtri, ma il caso frequente non richiede
            // di impostare niente.
            ApplyPeriod("month");
            _filter.PageSize = 25;

            await LoadCurrentUserAsync();
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

        private string BuildQuery()
        {
            var parts = new List<string>();

            if (_filter.DateFrom.HasValue) parts.Add($"dateFrom={_filter.DateFrom.Value:yyyy-MM-dd}");
            if (_filter.DateTo.HasValue) parts.Add($"dateTo={_filter.DateTo.Value:yyyy-MM-dd}");
            if (!string.IsNullOrEmpty(_filter.IdUserSpender)) parts.Add($"idUserSpender={Uri.EscapeDataString(_filter.IdUserSpender)}");
            if (_filter.Context != ExpenseContextFilter.All) parts.Add($"context={(int)_filter.Context}");
            if (_filter.NeedsConversion == true) parts.Add("needsConversion=true");
            if (!string.IsNullOrWhiteSpace(_filter.Search)) parts.Add($"search={Uri.EscapeDataString(_filter.Search)}");

            parts.Add($"pageNumber={_filter.PageNumber}");
            parts.Add($"pageSize={_filter.PageSize}");

            return string.Join("&", parts);
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
