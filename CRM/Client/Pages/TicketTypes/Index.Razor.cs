using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BlazoringComponents;
using Radzen;
using Microsoft.Extensions.Localization;
using Radzen.Blazor;
using CRM.Client.Models;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.TicketTypes
{
    [Authorize]
    public partial class Index: ComponentBase
    {
        

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        ITicketTypesService _service { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject] 
        IJSRuntime JSRuntime { get; set; }

        [Inject]
        INavMenuService navMenuService { get; set; }
        
        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

       
        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }
        
        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public string MessagePrepareDelete { get; set; }

        [Parameter]
        public bool CmdDetails { get; set; } = true;

        [Parameter]
        public bool CmdEdit { get; set; } = true;

        [Parameter]
        public bool CmdDelete { get; set; } = true;

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;


        private PagingHeaderModel _paging = new PagingHeaderModel();

        private TicketTypeFilter _filter = new TicketTypeFilter() { PageSize = 10, Skip = 0, Top = 10 };


        private string pagingSummaryFormat;

        private bool _isLoading = false;

        private RadzenDataGrid<TicketType> grdItemes;

        private IList<TicketType> _types = null;

        private PageHeaderModel? _pageHeader = null;

        protected override async void OnInitialized()
        {
            pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];
            await LoadData();

            _pageHeader = await HeaderService.Create(PageMode);

            StateHasChanged();
        }

        public async Task LoadData(LoadDataArgs args = null)
        {
            _isLoading = true;

            try
            {
                await GetItems(args);
            }

            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, ex.Message, ex.InnerException.Message);
            }

            finally
            {
                if (_types == null)
                    _types = Enumerable.Empty<TicketType>().ToList();
            }
        }

        public async Task GetItems(LoadDataArgs args = null)
        {
            try
            {
                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;

                    _filter.OrderBy = args?.OrderBy;
                    _filter.Filter = args?.Filter;
                }

                PagingResponse<TicketType> pagingResponse = await _service.Get(_filter);

                if (pagingResponse != null)
                {
                    _types = pagingResponse.Items;
                    _paging = pagingResponse.MetaData;
                }
                else
                    NotificationService.Notify(NotificationSeverity.Error, Localize["Errore"], Localize["Errore durante il download dei dati"]);
            }

            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (HttpRequestException ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, ex.Message, ex.InnerException.Message);
            }

            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, ex.Message, ex.InnerException.Message);
            }
            finally
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        protected void Details(int idTicketState)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(idTicketState);
            }
            else
                NavigationManager.NavigateTo($"/Settings/TicketTypes/{idTicketState}");
        }

        protected void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/Settings/TicketTypes/{id}/Edit");
        }

        protected void Cancel()
        {
            NavigationManager.NavigateTo("/Settings/TicketTypes");
        }
        protected void NewItem()
        {
            if (OnClickEdit != null)
                OnClickEdit(null);
            else
                NavigationManager.NavigateTo("/Settings/TicketTypes/New");
        }

        protected async Task Delete(TicketType item)
        {
            if (await DialogService.Confirm($"{Localize["Eliminare definitivamente il Tipo: "]} {item.Desc}?") == true)
            { 
                if (OnClickDelete != null)
                    OnClickDelete(item.Id);
                else
                {
                    await _service.Delete(item.Id);
                    await LoadData();
                }
            }
        }

        private void LanguageRow(TicketType item)
        {

            NavigationManager.NavigateTo($"Settings/TicketTypes/{item.Id}/TicketTypesLanguages");


        }

    }
}
