using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
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

namespace CRM.Client.Pages.TicketFeedbacks
{
    
    public partial class Index: ComponentBase
    {


        [Inject]
        private NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        ITicketFeedbackService Service { get; set; } = default!;

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; } = default!;

       

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

       

        private List<TicketFeedbackResponse>? _items = null;

        private int _totalCount = 0;

        private TicketFeedbackFilterModel _filter = new TicketFeedbackFilterModel();

        private bool _isLoading = false;

        private RadzenDataGrid<TicketFeedbackResponse> _grdItems = default!;

        [Display(Name = "search", ResourceType = typeof(CRM.Shared.Resources.App))]
        private string? _searchText = string.Empty;

        [Display(Name = "EventType", ResourceType = typeof(CRM.Shared.Resources.App))]
        private EventsTypes _eventType = EventsTypes.NullReference;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {

            await LoadDataAsync();
            
        }

        private async Task LoadDataAsync(LoadDataArgs? args = null)
        {
            _isLoading = true;
            if (args != null)
            {
                _filter.Skip = args?.Skip;
                _filter.Top = args?.Top;

                _filter.OrderBy = args?.OrderBy;
                _filter.Filter = args?.Filter;
            }
            
            // Usa il Service iniettato per i feedback, non LogEventService
            var resp = await Service.GetPagingAsync(_filter);

            _items = resp?.Items ?? new List<TicketFeedbackResponse>();
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
            NavigationManager.NavigateTo($"/{ConstHelper.ClientTicketFeedbacksPath}/{id}");
        }
       
    }
}
