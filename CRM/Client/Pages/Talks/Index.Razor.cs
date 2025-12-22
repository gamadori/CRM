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

namespace CRM.Client.Pages.Talks
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

       
        [Inject]
        private ITalkService _service { get; set; }

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

        private PagingResponse<TalkModel, decimal> _talks = null;

        private bool _isLoading = false;

        private RadzenDataGrid<TalkModel> grdTalks;

        private string _header = "Talks";

        private bool _filterState = false;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>() ;


        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private string? _idUser = null;

        protected async override Task OnInitializedAsync()
        {
            await LoadUsers();
            
            _bread = await BreadCrumbService.TalkUser(_idUser, false);
            _header = Localize["Talks"];

            await LoadData();

           
            StateHasChanged();
        }


        
        public async Task LoadData(LoadDataArgs args = null)
        {
            TalkFilter paging = new TalkFilter() { PageSize = 10, Skip = 0, Top = 10 }; ;
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

                _talks = await _service.Get<decimal>(paging);

                
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

                if (_talks == null)
                {
                    _talks = new PagingResponse<TalkModel, decimal>();

                    _talks.Items = new List<TalkModel>();
                    _talks.MetaData = new PagingHeaderModel();

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
        private void Details(int idTalk)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(idTalk);
            }
            else
            {
                if (IdProject != null)
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientTalkPath}/Info/{idTalk}/{IdProject}");
                else if (IdCompany != null)
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientTalkPath}/Info/Company/{idTalk}/{IdCompany}");
                else if (IdUser != null)
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientTalkPath}/Info/{idTalk}/{TypeSearch}/{IdUser}");
                else
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientTalkPath}/Info/{idTalk}");
            }
        }

        private void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientTalkPath}/Edit/{id}");
        }

        protected async Task Delete(int id)
        {

            if (await DialogService.Confirm(Localize["Eliminare il Talk selezionato"], Localize["Elimina"]) == true)
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
                NavigationManager.NavigateTo($"/{ConstHelper.ClientTalkPath}/Edit");
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
