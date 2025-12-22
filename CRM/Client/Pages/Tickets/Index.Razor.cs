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
using Syncfusion.Blazor.DropDowns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Tickets
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        ITicketService _service { get; set; }

        [Inject]
        IBaseRestService<ApplicationUser, UsersFilterModel, string> _serviceUser { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.Models.TicketStates> LocalizeTicketState { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IRestService<ApplicationUser> _userService { get; set; }

        [Inject]
        IBreadCrumbService BreadCrumbService { get; set; }

        [Inject]
        IJSRuntime JsRuntime { get; set; }

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
        public bool? Invoiced { get; set; }

        [Parameter]
        public string PageTitle { get; set; } = "Tickets";

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

       

        private PagingResponse<TicketModel, string> _ticketView = null;

        private bool _isLoading = false;

        private RadzenDataGrid<TicketModel> grdTickets;

        private string _header = "Tickets";

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>() ;

        private int? _stateType = null;

        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private bool _filterState = false;

        private string _filterCompany = null;

        private string? _idUser = null;

        private ApplicationUser _user;

        private bool _isMobile = false;

        private bool _isResponsable = false;

        private string _userFilter = null;

        private IList<TicketModel> _selectedTicket = new List<TicketModel>();

        private int _numRecords = 0;

        protected async override Task OnInitializedAsync()
        {
            //#if DEBUG
            //            await Task.Delay(10000);
            //#endif

            //navMenuService.CallRequestRefresh();
            await FindResponsiveness();
            _user = await _userService.Get();
            
            if (IdCompany == null)
            {
                _header = Localize["Tickets"];
            }
            
            await LoadUsers();
            if (IdUser != null)
            {
                _filterState = true;
                _idUser = IdUser;
            }
            await LoadData();

            StateHasChanged();
        }

        private async Task LoadDataAllUser()
        {
            _idUser = null;
            await LoadData();
        }

        private async Task LoadDataTickets(object param = null)
        {
           
            TypeSearch = (int)TicketTypeSearch.All;
            await LoadData();
        }

        
        public async Task LoadData(LoadDataArgs args = null)
        {
            TicketFilter paging = new TicketFilter() { PageSize = 10, Skip = 0, Top = 10 }; ;
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
                paging.TypeSearch = TypeSearch;
                paging.IdArticle = IdArticle;
                paging.IdCompany = IdCompany;
                paging.IdProject = IdProject;

                if (_filterState)
                    paging.IdUserAssigned = _idUser;
                else
                    paging.IdUserAssigned = null;

                //var resp = await RestClientHelper.Get(HttpClient, ConstHelper.TicketPath, paging);
                
                _ticketView = await _service.Get<string>(paging);

                _numRecords = _ticketView.MetaData.TotalCount;

                // _bread = await BreadCrumbService.Ticket(_idUser, LoadDataTickets);
                _bread = await BreadCrumbService.TicketAssigned(_idUser, (TicketTypeSearch)TypeSearch, false, LoadDataTickets);
                //_ticketView = await Decode(resp);
                
                
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

                if (_ticketView == null)
                {
                    _ticketView = new PagingResponse<TicketModel, string>();

                    _ticketView.Items = new List<TicketModel>();
                    _ticketView.MetaData = new PagingHeaderModel();
                    _ticketView.Total = null;
                }
                _isLoading = false;
                StateHasChanged();
            }

        }

        void OnCellClick(DataGridCellMouseEventArgs<TicketModel> args)
        {
           
                Select(args);
            
        }

        private void Select(DataGridCellMouseEventArgs<TicketModel> args)
        {
           
            var ticket = _selectedTicket.FirstOrDefault(i => i.Id == args.Data.Id);
            if (ticket != null)
            {
                _selectedTicket.Remove(ticket);
            }
            else
            {
                _selectedTicket.Add(args.Data);
            }
        }

        void OnCellRender(DataGridCellRenderEventArgs<TicketModel> args)
        {
            
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
        private void Details(int? idTicket)
        {
            if (idTicket == null)
                return;

            if (OnClickDetails != null)
            {
                OnClickDetails(idTicket.Value);
            }
            else
            {
                if (IdProject != null)
                    NavigationManager.NavigateTo($"/Tickets/Info/{idTicket}/{IdProject}");
                else if (IdCompany != null)
                    NavigationManager.NavigateTo($"/Tickets/Info/Company/{idTicket}/{IdCompany}");
                else if (_idUser != null)
                    NavigationManager.NavigateTo($"/Tickets/Info/{idTicket}/{TypeSearch}/{IdUser}");
                else if (TypeSearch != (int)TicketTypeSearch.All)
                {
                    NavigationManager.NavigateTo($"/Tickets/Info/filter/{idTicket}/{TypeSearch}");
                }
                else
                    NavigationManager.NavigateTo($"/Tickets/Info/{idTicket}");
            }
        }

        private void Edit(int? id)
        {
            if (id == null)
                return;

            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/Tickets/Edit/{id}/{TypeSearch}-{IdUser}");
        }

        protected async Task Delete(int? id)
        {

            if (id == null)
                return;

            if (await DialogService.Confirm(Localize["Eliminare l'intervento selezionato"], Localize["Elimina"]) == true)
            {
                if (OnClickDelete != null)
                    OnClickDelete(id.Value);
                else
                {
                    await _service.Delete(id.Value);

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
            {
                if (_user.IsClient)
                {
                    NavigationManager.NavigateTo($"/Tickets/Create");
                }
                else
                {
                    NavigationManager.NavigateTo($"/Tickets/Edit");
                }
            }
        }

        private string Header()
        {
            switch ((TicketTypeSearch)TypeSearch)
            {
                case TicketTypeSearch.Working:
                    return Localize["Tickets in Lavorazioni"];

                case TicketTypeSearch.Closed:
                    return Localize["Tickets Chiusi"];

            }
            return "Tickets";
        }

        private void CellRender(DataGridCellRenderEventArgs<TicketModel> args)
        {
           
            if (args.Column.Property == nameof(TicketModel.State))
            {
                args.Attributes.Add("style", $"background-color: {args.Data.StateColor};");

                
            }
            else if (_selectedTicket.Any(i => i.Id == args.Data.Id))
            {
                args.Attributes.Add("style", $"background-color: var(--rz-secondary-lighter);");
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

        public async Task FindResponsiveness()
        {
            _isMobile = await JsRuntime.InvokeAsync<bool>("isDevice");

            //if (_isMobile)
            //    _buttonSize = ButtonSize.Small;
        }

        private bool ColVisible()
        {
            return !_isMobile || _isResponsable;
        }
    }
}
