using CRM.Client.Helpers;
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

namespace CRM.Client.Pages.ContractTypeTicketTypes
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

       
        [Inject]
        private IContractTypeTicketService Service { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IRestService<ApplicationUser> UserSignedService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public int IdContractType { get; set; }

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

        [Parameter]
        public bool BreadCrumbVisible { get; set; } = true;

        private PagingResponse<ContractTypeTicketTypeModel> _items = null;

        private bool _isLoading = false;

        private RadzenDataGrid<ContractTypeTicketTypeModel> _grdContractTypeTicket;

        private string _header = "Contract Details";

        private bool _filterState = false;


        private ApplicationUser? _user;

        private FilterMode _filterMode = FilterMode.Advanced;

        protected async override Task OnInitializedAsync()
        {

            _header = Localize["Contract Details"];

            _user = await UserSignedService.Get();

            if (PageMode == PageModality.Dialog)
                _filterMode = FilterMode.SimpleWithMenu;

            await LoadData();
            
            StateHasChanged();
        }

        
        public async Task LoadData(LoadDataArgs args = null)
        {
            ContractTypeTicketTypeFilter paging = new ContractTypeTicketTypeFilter() { PageSize = 10, Skip = 0, Top = 10 }; ;
            _isLoading = true;

            try
            {
                paging.IdContractType = IdContractType;
                
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

                _items = await Service.Get(paging);
                
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

                if (_items == null)
                {
                    _items = new PagingResponse<ContractTypeTicketTypeModel>();

                    _items.Items = new List<ContractTypeTicketTypeModel>();
                    _items.MetaData = new PagingHeaderModel();

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
                
                NavigationManager.NavigateTo($"/{ConstHelper.ClientContractTypeTicketsPath}/{id}/Details");
            }
        }

        private void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientContractTypeTicketsPath}/{id}/Edit");
        }

        protected async Task Delete(int id)
        {

            if (await DialogService.Confirm(Localize["Delete the selected Contract"], Localize["Delete"]) == true)
            {
                if (OnClickDelete != null)
                    OnClickDelete(id);
                else
                {
                    await Service.Delete(id);

                    await LoadData();
                    StateHasChanged();
                }
            }
        }
        private async void NewItem()
        {
            if (OnNewItem.HasDelegate)
                await OnNewItem.InvokeAsync();
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientContractTypeTicketsPath}/New");
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

            NavigationManager.NavigateTo($"{ConstHelper.ClientContractTypeTicketsPath}/Index/{id}");


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
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientContractTypeTicketsPath}/{id}");
                    break;
            }

        }
    }
}
