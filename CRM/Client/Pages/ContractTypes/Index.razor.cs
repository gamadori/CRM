using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ContractTypes
{
    public partial class Index : ComponentBase
    {


        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        private IContractTypesService Service { get; set; }


        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }


        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public Action<int> OnClickGantt { get; set; }

        private string _pagingSummaryFormat;

        private List<ContractType> _items = null;

        private PagingHeaderModel _paging;
        private ContractTypeFilter _filter = new ContractTypeFilter();

        private RadzenDataGrid<ContractType> _grdContractTypes;


        private PageHeaderModel _pageHeader = null;

        private bool _isLoading = false;

        protected override async Task OnInitializedAsync()
        {


            _isLoading = true;

            _pageHeader = await HeaderService.Create();

            _pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];

            await LoadData();
        }

        public async Task LoadData(LoadDataArgs args = null)
        {

            _isLoading = true;

            var template = Enumerable.Empty<ContractType>().AsQueryable();

            try
            {
                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;
                    _filter.Filter = args?.Filter;
                    _filter.OrderBy = args?.OrderBy;
                }
                var pagingResponse = await Service.Get(_filter);

                _items = pagingResponse.Items;
                _paging = pagingResponse.MetaData;
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex);

            }
            finally
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }

        }



        protected void NewItem()
        {
            NavigationManager.NavigateTo($"/{ConstHelper.ClientContractTypesPath}/Edit");
        }

       

        protected async Task Delete(ContractType item)
        {
          

            if (item != null)
            {
                if (await DialogService.Confirm(string.Format(Localize["Delete the Contract Type {0}?"], item.Name)) == true)
                {
                    await Service.Delete(item.Id);
                    await LoadData();
                }
            }
            
            StateHasChanged();

        }
        
        protected void Details(int id)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(id);
            }
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientContractTypesPath}/{id}");
        }


        protected void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientContractTypesPath}/{id}/Edit");
        }

    }
}
