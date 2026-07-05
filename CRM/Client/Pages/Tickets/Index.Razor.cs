using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
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
        ITicketsService _service { get; set; }

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


        private PagingResponse<TicketDTO, string> _ticketView = null;

        private bool _isLoading = false;

        private RadzenDataGrid<TicketDTO> grdTickets;

        private string _header = "Tickets";

        private int? _stateType = null;

        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private bool _filterState = false;

        private string _filterCompany = null;

        private string? _idUser = null;

        private ApplicationUser _user;

        private bool _isMobile = false;

        private bool _isResponsable = true;

        private string _userFilter = null;

        private IList<TicketDTO> _selectedTicket = new List<TicketDTO>();

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

            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

                if (_ticketView == null)
                {
                    _ticketView = new PagingResponse<TicketDTO, string>();

                    _ticketView.Items = new List<TicketDTO>();
                    _ticketView.MetaData = new PagingHeaderModel();
                    _ticketView.Total = null;
                }
                _isLoading = false;
                StateHasChanged();
            }

        }

        void OnCellClick(DataGridCellMouseEventArgs<TicketDTO> args)
        {
           
                Select(args);
            
        }

        private void Select(DataGridCellMouseEventArgs<TicketDTO> args)
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

        void OnCellRender(DataGridCellRenderEventArgs<TicketDTO> args)
        {
            
        }

        private async Task LoadUsers()
        {
            UsersFilterModel request = new UsersFilterModel();


            var response = await _serviceUser.GetList(request);

            _users = response.Items.ToList();

            
        }

      

        private async Task<PagingResponse<TicketDTO>> Decode(HttpResponseMessage resp)
        {
            if (resp.IsSuccessStatusCode)
            {
                var content = await resp.Content.ReadAsStringAsync();
                var item = JsonSerializer.Deserialize<ObjectView<TicketDTO, string>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var pagingResponse = new PagingResponse<TicketDTO>()
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

        private void CellRender(DataGridCellRenderEventArgs<TicketDTO> args)
        {
            if (args.Column?.Property == nameof(TicketDTO.State) && args.Data != null)
            {
                var textColor = GetContrastColor(args.Data.StateColor);
                var backgroundColor = string.IsNullOrWhiteSpace(args.Data.StateColor)
                    ? "transparent"
                    : args.Data.StateColor;

                args.Attributes.Add(
                    "style",
                    $"background-color: {backgroundColor} !important; color: {textColor} !important;");
            }
            else if (args.Data != null && _selectedTicket?.Any(i => i.Id == args.Data.Id) == true)
            {
                args.Attributes.Add("style", $"background-color: var(--rz-secondary-lighter);");
            }
        }

        private static string GetStateTextStyle(string? backgroundColor)
        {
            return $"color: {GetContrastColor(backgroundColor)} !important; font-weight: 700;";
        }

        /// <summary>
        /// Restituisce il colore del testo con il contrasto WCAG migliore rispetto allo sfondo.
        /// </summary>
        private static string GetContrastColor(string? backgroundColor)
        {
            if (!TryParseHexColor(backgroundColor, out var red, out var green, out var blue))
                return "#111827";

            var luminance = 0.2126 * ToLinearRgb(red)
                          + 0.7152 * ToLinearRgb(green)
                          + 0.0722 * ToLinearRgb(blue);

            var contrastWithDarkText = (luminance + 0.05) / 0.05;
            var contrastWithWhiteText = 1.05 / (luminance + 0.05);

            return contrastWithDarkText >= contrastWithWhiteText ? "#111827" : "#ffffff";
        }

        private static bool TryParseHexColor(string? color, out byte red, out byte green, out byte blue)
        {
            red = green = blue = 0;

            if (string.IsNullOrWhiteSpace(color))
                return false;

            var hex = color.Trim().TrimStart('#');
            if (hex.Length == 3)
                hex = string.Concat(hex.Select(character => $"{character}{character}"));

            if (hex.Length != 6)
                return false;

            return byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out red)
                && byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out green)
                && byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out blue);
        }

        private static double ToLinearRgb(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
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
