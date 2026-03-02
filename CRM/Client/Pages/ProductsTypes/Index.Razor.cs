using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen.Blazor;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CRM.Shared.Resources.Models;
using System.Threading;
using CRM.Client.Models;
using CRM.Shared.DTOs;

namespace CRM.Client.Pages.ProductsTypes
{
    [Authorize]
    public partial class Index: ComponentBase
    {

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        
        [Inject]
        IProductTypesService Service { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        private IList<ProductTypeDTO> _productTypes = null;


        private PagingHeaderModel _paging = new PagingHeaderModel();

        private ProductTypeFilter _filter = new ProductTypeFilter() { PageSize = 10, Skip = 0, Top = 10 };

        private ProductTypeDTO _productType;

        private string _pagingSummaryFormat;

        private int _pageSize = 10;

        private bool _isLoading = false;

        private RadzenDataGrid<ProductTypeDTO> grdItems;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
            
            _pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];
            await LoadData();

            _pageHeader = await HeaderService.Create();
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
                if (_productTypes == null)
                    _productTypes = Enumerable.Empty<ProductTypeDTO>().ToList();


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

                PagingResponse<ProductTypeDTO> pagingResponse = await Service.GetPagingAsync(_filter); //await _serviceProductType.Get(_filter);

                if (pagingResponse != null)
                {
                    _productTypes = pagingResponse.Items;
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


        protected void Details(int id)
        {
            NavigationManager.NavigateTo($"/Settings/ProductsTypes/{id}/Details");
        }

        protected void Edit(int id)
        {
            NavigationManager.NavigateTo($"/Settings/ProductsTypes/{id}/Edit");
        }
        protected void NewItem()
        {
            NavigationManager.NavigateTo("/Settings/ProductsTypes/New");
        }

        protected async Task Delete(ProductTypeDTO item)
        {
            if (await DialogService.Confirm($"{Localize["Eliminare il Tipo Prodotto:"]} {item.Name}") == true)
            {
                await Service.DeleteAsync(item.Id);   

                await LoadData();
            }

            
        }

       

        protected void ImportData()
        {
            NavigationManager.NavigateTo($"/CSVSettings/CSVData/{CSVTable.Company.ToString()}");
        }

        
       
    }
}
