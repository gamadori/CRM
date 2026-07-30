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

namespace CRM.Client.Pages.Accessories
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

       
        [Inject]
        private IAccessoriesService AccessoriesService { get; set; }

        [Inject]
        ICurrentUserService _userSigned { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

       
        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }


        [Parameter]
        public int? IdAccessoryType { get; set; }

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public Action<int> OnGotoIndex { get; set; }


        private PagingResponse<Accessory> _accessories = null;

        private bool _isLoading = false;

        private RadzenDataGrid<Accessory> grdAccessory;

        private string _header = "Accessories";

        private bool _filterState = false;

        private PageHeaderModel _pageHeader = null;

        private ApplicationUser? _user;

        protected async override Task OnInitializedAsync()
        {
            if (IdAccessoryType == null)
                _pageHeader = await HeaderService.Create();
            _header = Localize["Accessories"];

            _user = await _userSigned.Get();

            await LoadData();
            
            StateHasChanged();
        }

        
        public async Task LoadData(LoadDataArgs args = null)
        {
            AccessoryFilter paging = new AccessoryFilter() { PageSize = 10, Skip = 0, Top = 10 }; ;
            _isLoading = true;

            try
            {

                paging.IdAccessoryType = IdAccessoryType;

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
               
                _accessories = await AccessoriesService.Get(paging);
                
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

                if (_accessories == null)
                {
                    _accessories = new PagingResponse<Accessory>();

                    _accessories.Items = new List<Accessory>();
                    _accessories.MetaData = new PagingHeaderModel();

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
                NavigationManager.NavigateTo($"/{ConstHelper.ClientAccessoriesPath}/{id}/Details");
            }
        }

        private void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientAccessoriesPath}/{id}/Edit");
        }

        private void NewAccessory()
        {
            if (OnClickEdit != null)
                OnClickEdit(null);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientAccessoriesPath}/New");
        }


        protected async void OnChangeFilter(bool state)
        {

            
            await LoadData();
            StateHasChanged();
        }

        private async Task Delete(int id)
        {
            if (await DialogService.Confirm(Localize["Eliminare l'accessorio selezionato"], Localize["Elimina"]) == true)
            {
                if (OnClickDelete != null)
                    OnClickDelete(id);
                else
                {
                    await AccessoriesService.Delete(id);
                    await LoadData();
                    StateHasChanged();
                }
            }
        }

    }
}
