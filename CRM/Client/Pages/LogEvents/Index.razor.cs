using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;
using static CRM.Shared.LogEvent;

namespace CRM.Client.Pages.LogEvents
{
    
    public partial class Index: ComponentBase
    {


        [Inject]
        private NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        ILogEventService LogEventService { get; set; } = default!;

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; } = default!;

        [Inject]
        IHeaderService HeaderService { get; set; } = default!;

        

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

       

        private List<LogEvent>? _items = null;

        private int _totalCount = 0;

        private LogEventFilterModel _filter = new LogEventFilterModel();

        private bool _isLoading = false;

        private RadzenDataGrid<LogEvent> _grdLogs = default!;

        [Display(Name = "search", ResourceType = typeof(CRM.Shared.Resources.App))]
        private string? _searchText = string.Empty;

        [Display(Name = "EventType", ResourceType = typeof(CRM.Shared.Resources.App))]
        private EventsTypes _eventType = EventsTypes.NullReference;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {

            await LoadDataAsync();
            _pageHeader = await HeaderService.Create(PageMode);
        }

        private async Task LoadDataAsync(LoadDataArgs? args = null)
        {
            _isLoading = true;

            // Sempre valorizzati: senza Skip/Top il server restituisce tutti i record
            // (grid in modalità server-side, mostra esattamente ciò che riceve in Data).
            _filter.Skip = args?.Skip ?? 0;
            _filter.Top = args?.Top ?? ConstHelper.PageSize;
            _filter.OrderBy = args?.OrderBy;
            _filter.Filter = args?.Filter;

            _filter.Message = _searchText;
            _filter.EventType = _eventType;
            var resp = await LogEventService.GetPagingAsync(_filter);

            _items = resp?.Items ?? new List<LogEvent>();
            _totalCount = resp?.MetaData?.TotalCount ?? 0;
            _isLoading = false;

            StateHasChanged();
        }

        private async Task OnSearchTextChanged(string? search)
        {
            _searchText = search;
            
            await LoadDataAsync();
        }

        private async Task OnEventTypeChanged()
        {
            
            await LoadDataAsync();
        }


        private void OnDetails(int id)
        {
            NavigationManager.NavigateTo($"/{ConstHelper.ClientLogEventsPath}/{id}");
        }

        /// <summary>
        /// Colora le righe del grid in base al tipo di evento
        /// </summary>
        private void OnRowRender(RowRenderEventArgs<LogEvent> args)
        {
            var eventClass = args.Data.EventType switch
            {
                EventsTypes.Info => "log-event-info",
                EventsTypes.Warning => "log-event-warning",
                EventsTypes.Error => "log-event-error",
                EventsTypes.Permits => "log-event-permits",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(eventClass))
                args.Attributes["class"] = eventClass;
        }
    }
}
