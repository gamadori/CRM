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

        [Inject]
        IHeaderService HeaderService { get; set; }

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

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;


        private PagingResponse<TicketModel, string> _ticketView = null;

        private bool _isLoading = false;

        private RadzenDataGrid<TicketModel> grdTickets;

        private string _header = "Tickets";

        private List<BreadcrumbItem> _breadcrumbItems = new List<BreadcrumbItem>();

        private int? _stateType = null;

        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private bool _filterState = false;

        private string _filterCompany = null;

        private string? _idUser = null;

        private ApplicationUser _user;

        private bool _isMobile = false;

        private bool _isResponsable = true;

        private string _userFilter = null;

        private IList<TicketModel> _selectedTicket = new List<TicketModel>();

        private int _numRecords = 0;

        private PageHeaderModel? _pageHeader = null;

        protected async override Task OnInitializedAsync()
        {
           
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

           
            _pageHeader = await HeaderService.Create(PageMode);
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

                if (_idUser != null)
                    paging.IdUserAssigned = _idUser;
                else
                    paging.IdUserAssigned = null;

                
                _ticketView = await _service.Get<string>(paging);

                _numRecords = _ticketView.MetaData.TotalCount;

                _breadcrumbItems = BreadCrumbService.TicketAssigned(_idUser, (TicketTypeSearch)TypeSearch, LoadDataTickets);
                
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
                    NavigationManager.NavigateTo($"/Tickets/{idTicket}/Info/{IdProject}");

                else if (IdCompany != null)
                    NavigationManager.NavigateTo($"Companies/{IdCompany}/Tickets/{idTicket}");

                else if (_idUser != null)
                    NavigationManager.NavigateTo($"/Tickets/{idTicket}/{TypeSearch}/{_idUser}");

                else if (TypeSearch != (int)TicketTypeSearch.All)
                {
                    NavigationManager.NavigateTo($"/Tickets/{idTicket}/filter/{TypeSearch}");
                }
                else
                    NavigationManager.NavigateTo($"/Tickets/{idTicket}/Info");
            }
        }

        private void Edit(int? id)
        {
            if (id == null)
                return;

            if (OnClickEdit != null)
                OnClickEdit(id);
            else
            {
                if (TypeSearch != (int)TicketTypeSearch.All && IdUser != null)
                    NavigationManager.NavigateTo($"/Tickets/{id}/Edit/{TypeSearch}-{IdUser}");
                else 
                    NavigationManager.NavigateTo($"/Tickets/{id}/Edit");
            }
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
                    NavigationManager.NavigateTo($"/Tickets/New");
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
                var textColor = GetContrastColor(args.Data.StateColor);
                args.Attributes.Add("style", $"background-color: {args.Data.StateColor}; color: {textColor};");

                
            }
            else if (_selectedTicket.Any(i => i.Id == args.Data.Id))
            {
                args.Attributes.Add("style", $"background-color: var(--rz-secondary-lighter);");
            }

        }

        /// <summary>
        /// Calcola il colore del testo (bianco o nero) in base alla luminosità del colore di sfondo
        /// </summary>
        private string GetContrastColor(string backgroundColor)
        {
            if (string.IsNullOrWhiteSpace(backgroundColor))
                return "#000000";

            // Rimuovi il # se presente
            var hex = backgroundColor.TrimStart('#');

            // Gestisci formati shorthand (#RGB -> #RRGGBB)
            if (hex.Length == 3)
            {
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            }

            // Assicurati che sia un hex valido
            if (hex.Length != 6)
                return "#000000";

            try
            {
                // Converti hex in RGB
                var r = Convert.ToInt32(hex.Substring(0, 2), 16);
                var g = Convert.ToInt32(hex.Substring(2, 2), 16);
                var b = Convert.ToInt32(hex.Substring(4, 2), 16);

                // Calcola la luminosità relativa usando la formula WCAG
                // https://www.w3.org/TR/WCAG20/#relativeluminancedef
                var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

                // Se la luminosità è > 0.5, usa testo nero, altrimenti bianco
                return luminance > 0.5 ? "#000000" : "#ffffff";
            }
            catch
            {
                return "#000000";
            }
        }

        protected async void OnChangeFilter()
        {

            
            await LoadData();
            StateHasChanged();
        }

        private async Task OnChangeIdUser()
        {
            IdUser = _idUser;
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
