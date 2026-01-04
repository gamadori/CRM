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

namespace CRM.Client.Pages.Deals
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

       
        [Inject]
        private IDealService _service { get; set; }

        [Inject]
        IBaseRestService<ApplicationUser, UsersFilterModel, string> _serviceUser { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IEnumService EnumService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IBreadCrumbService BreadCrumbService { get; set; }


        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public string? IdUser { get; set; }

        [Parameter]
        public int?  IdArticle { get; set; }

        [Parameter]
        public int? IdProject { get; set; }

        [Parameter]
        public int TypeSearch { get; set; } = (int)TicketTypeSearch.All;



        [Parameter]
        public string PageTitle { get; set; } = "Tickets";

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public Action<int> OnGotoIndex { get; set; }

        private PagingResponse<DealModel, decimal> _deals = null;

        private bool _isLoading = false;

        private RadzenDataGrid<DealModel> grdDeals;

        private string _header = "Deals";

        private bool _filterState = false;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>() ;


        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private string? _idUser = null;

        protected async override Task OnInitializedAsync()
        {
            await LoadUsers();
            
            _bread = await BreadCrumbService.DealUser(_idUser, false);
            _header = Localize["Deals"];

            await LoadData();

           
            StateHasChanged();
        }


        
        public async Task LoadData(LoadDataArgs args = null)
        {
            DealFilter paging = new DealFilter() { PageSize = 10, Skip = 0, Top = 10 }; ;
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
                paging.IdUser = IdUser;

                _deals = await _service.Get<decimal>(paging);

                
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

                if (_deals == null)
                {
                    _deals = new PagingResponse<DealModel, decimal>();

                    _deals.Items = new List<DealModel>();
                    _deals.MetaData = new PagingHeaderModel();

                }
                _isLoading = false;
            }

        }

        private async Task LoadUsers()
        {
            UsersFilterModel request = new UsersFilterModel();


            var response = await _serviceUser.GetList(request);

            _users = response.Items.ToList();

            
        }

        private async Task<PagingResponse<TicketModel>> Decode(HttpResponseMessage resp)
        {
            if (resp.IsSuccessStatusCode)
            {
                var content = await resp.Content.ReadAsStringAsync();
                var item = JsonSerializer.Deserialize<ObjectView<TicketModel, string>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var pagingResponse = new PagingResponse<TicketModel>()
                {
                    Items = item.Items,
                    MetaData = JsonSerializer.Deserialize<PagingHeaderModel>(resp.Headers
                        .GetValues(ConstHelper.PagingHeader).First(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }),
                    Total = item.Total
                };
                return pagingResponse;
            }
            else
                return null;
            
        }
        private void Details(int idDeal)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(idDeal);
            }
            else
            {
                if (IdProject != null)
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/Info/{idDeal}/{IdProject}");
                else if (IdCompany != null)
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/Info/Company/{idDeal}/{IdCompany}");
                else if (IdUser != null)
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/Info/{idDeal}/{TypeSearch}/{IdUser}");
                else
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/Info/{idDeal}");
            }
        }

        private void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/{id}/Edit");
        }

        protected async Task Delete(int id)
        {

            if (await DialogService.Confirm(Localize["Eliminare il Deal selezionato"], Localize["Elimina"]) == true)
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
        private void NewTicket()
        {
            if (OnClickEdit != null)
                OnClickEdit(null);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/New");
        }

        private void CellRender(DataGridCellRenderEventArgs<TicketModel> args)
        {
           
            if (args.Column.Property == nameof(TicketModel.State))
            {
                args.Attributes.Add("style", $"background-color: {args.Data.StateColor};");

                
            }

            
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

        
    }
}
