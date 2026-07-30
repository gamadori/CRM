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
using Radzen.Blazor.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.AccessoryTypes
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

       
        [Inject]
        private IAccessoryTypesService _service { get; set; }

        [Inject]
        ICurrentUserService _userSigned { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

       
        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }


        [Parameter]
        public EventCallback OnNewItem { get; set; }

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public Action<int> OnGotoIndex { get; set; }

        [Parameter]
        public EventCallback<int?> OnSelectItem { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private PagingResponse<AccessoryTypeModel> _accessoryTypes = null;

        private bool _isLoading = false;

        private RadzenDataGrid<AccessoryTypeModel> grdAccessoryTypes;

        private string _header;

        private bool _filterState = false;

        private PageHeaderModel _pageHeader = null;

        private ApplicationUser? _user;

        private FilterMode _filterMode = FilterMode.Advanced;

        protected async override Task OnInitializedAsync()
        {
            if (PageMode != PageModality.Dialog)
                _pageHeader = await HeaderService.Create();

            _header = Localize["Details"];

            _user = await _userSigned.Get();

            if (PageMode == PageModality.Dialog)
                _filterMode = FilterMode.SimpleWithMenu;

            await LoadData();
            
            StateHasChanged();
        }

        
        public async Task LoadData(LoadDataArgs args = null)
        {
            AccessoryTypeFilter paging = new AccessoryTypeFilter() { PageSize = 10, Skip = 0, Top = 10 }; ;
            _isLoading = true;

            try
            {
                
                if (args != null)
                {
                    paging.Skip = args.Skip;
                    paging.Top = args.Top;
                    paging.OrderBy = args.OrderBy;

                    if (args.Filters != null && args.Filters.Any())
                    {
                        if (paging.Filter?.Length > 0)
                            paging.Filter += " And ";

                        paging.Filter += args.Filter;
                    }
                }
               
                _accessoryTypes = await _service.Get(paging);
                
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

                if (_accessoryTypes == null)
                {
                    _accessoryTypes = new PagingResponse<AccessoryTypeModel>();

                    _accessoryTypes.Items = new List<AccessoryTypeModel>();
                    _accessoryTypes.MetaData = new PagingHeaderModel();

                }
                _isLoading = false;
            }

        }

       

        
        private void Details(int id)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(id);
            }
            else
            {
                
                NavigationManager.NavigateTo($"/{ConstHelper.ClientAccessoryTypesPath}/{id}");
            }
        }

        private void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientAccessoryTypesPath}/{id}/Edit");
        }

        protected async void NewAccessoryType()
        {
            if (OnNewItem.HasDelegate)
                await OnNewItem.InvokeAsync();
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientAccessoryTypesPath}/New");
        }


        protected async void OnChangeFilter(bool state)
        {

            
            await LoadData();
            StateHasChanged();
        }

        private async Task OnChangeIdUser()
        {
            await LoadData();
        }

        private void LanguageRow(int id)
        {

            NavigationManager.NavigateTo($"{ConstHelper.ClientAccessoryTypeLangsPath}/Index/{id}");


        }

        private async Task OnClickName(int? id)
        {
            switch (PageMode)
            {
                case PageModality.Dialog:

                    if (OnSelectItem.HasDelegate)
                    {
                        await OnSelectItem.InvokeAsync(id);
                    }
                    else
                        DialogService.CloseSide(id);
                    break;
                case PageModality.Visualization:
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientAccessoryTypesPath}/{id}");
                    break;
            }

        }

        private async Task Delete(int id)
        {
            if (await DialogService.Confirm(Localize["Eliminare il Tipo selezionato"], Localize["Elimina"]) == true)
            {
                if (OnClickDelete != null)
                    OnClickDelete(id);
                else
                {
                    await _service.Delete(id);
                    await LoadData();
                    StateHasChanged();
                }
            }
        }

    }
}
