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
using Microsoft.Extensions.Localization;



namespace CRM.Client.Pages.Groups
{
    [Authorize]
    public partial class Index: ComponentBase
    {
        
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject] 
        private IJSRuntime JSRuntime { get; set; }

       
        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        Radzen.DialogService DialogService { get; set; }

        [Parameter]
        public int? IdTicketType { get; set; } = null;

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
        public bool BreadCrumbVisible { get; set; } = true;

        private List<CRM.Shared.Group> _groups = null;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private GroupFilter _filter = new GroupFilter();


        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        private string _pagingSummaryFormat;

        private string _pageTitle;

        private bool _isLoading = false;

        private string _pageMessage;

        private Radzen.Blazor.RadzenDataGrid<CRM.Shared.Group> _grdGroup;

        protected override async Task OnInitializedAsync()
        {
            //#if DEBUG
            //            await Task.Delay(10000);
            //#endif
            _pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];
 
            _bread.Add(new BreadcrumbModel() { Title = Localize["Settings"], Url = "Settings" });
            _bread.Add(new BreadcrumbModel() { Title = Localize["Gruppi"], Url = null});
            _pageTitle = Localize["Gruppi"];

           await LoadData();
           await base.OnInitializedAsync();
        }

        

        

        public async Task LoadData(Radzen.LoadDataArgs args = null)
        {
            _isLoading = true;


            try
            {
                _filter.IdTicketType = IdTicketType;

                

                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;

                    _filter.OrderBy = args?.OrderBy;
                }
                _filter.PageSize = ConstHelper.PageSize;
                var pagingResponse = await RestClientService.Get<Group, GroupFilter>(_filter, ConstHelper.GroupsPath);

                _groups = pagingResponse.Items;
                _paging = pagingResponse.MetaData;

            }

            catch (Exception ex)
            {

                _pageMessage = ex.Message;
            }
            finally
            {
                _isLoading = false;
                if (_groups == null)
                    _groups = new List<CRM.Shared.Group>();

                await InvokeAsync(StateHasChanged);
            }

        }

        

        private void Details(int id)
        {
            NavigationManager.NavigateTo($"/Settings/Groups/info/{id}");
        }
        protected void Edit(int id)
        {
            NavigationManager.NavigateTo($"/Settings/Groups/Edit/{id}");
        }
        protected void Cancel()
        {
            NavigationManager.NavigateTo("/Settings/Groups");
        }
        protected void NewItem()
        {
            if (OnClickEdit != null)
                OnClickEdit(null);
            else
                NavigationManager.NavigateTo("/Settings/Groups/Edit");
        }

        protected async Task Delete(CRM.Shared.Group group)
        {
           
            if (await DialogService.Confirm($"{Localize["Eliminare il Gruppo"]}: {group.Name}?") == true)
            {
                if (OnClickDelete != null)
                {
                    OnClickDelete(group.Id);
                }
                else
                {
                    await RestClientService.Delete<int>(group.Id, ConstHelper.GroupsPath);

                    await LoadData();
                }
            }
        }

        
    }
}
