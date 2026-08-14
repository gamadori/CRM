using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
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
        ICompaniesService _serviceCompany { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.Models.TicketStates> LocalizeTicketState { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        ICurrentUserService _userService { get; set; }

        [Inject]
        IJSRuntime JsRuntime { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public string? IdUser { get; set; }

        [Parameter]
        public int?  IdArticle { get; set; }

        [Parameter]
        public int? IdDeal { get; set; }

        [Parameter]
        public int? IdCommessaFase { get; set; }

        [Parameter]
        public int? IdCommessa { get; set; }

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

        private List<CompanyDTO> _companies = new List<CompanyDTO>();

        private bool _filterState = false;

        private string _filterCompany = null;

        private string? _idUser = null;

        private ApplicationUser _user;

        private bool _isMobile = false;

        private bool _isResponsable = true;

        private string _userFilter = null;

        private int? _companyFilter = null;

        private IList<TicketDTO> _selectedTicket = new List<TicketDTO>();

        private int _numRecords = 0;

        private PageHeaderModel? _pageHeader = null;

        private int? _claimingTicketId = null;

        /// <summary>
        /// Natura del lavoro: null = tutto, true = solo commessa, false = solo assistenza. Il filtro
        /// e' applicato dal server (TicketFilter.HasCommessa), come nella pianificazione ticket.
        /// </summary>
        private bool? _hasCommessa = null;

        /// <summary>Filtri di rotta con cui la griglia e' stata caricata l'ultima volta.</summary>
        private int? _loadedTypeSearch = null;

        private string _loadedIdUser = null;

        protected async override Task OnInitializedAsync()
        {

            await FindResponsiveness();
            _user = await _userService.Get();

            if (IdCompany == null)
            {
                _header = Localize["Tickets"];
            }

            await LoadUsers();
            await LoadCompanies();

            await ApplyRouteFiltersAsync();
        }

        /// <summary>
        /// Quando cambiano solo i parametri di rotta (per esempio da /Tickets/Index/1 a
        /// /Tickets/Index/9) Blazor riusa la stessa istanza e OnInitializedAsync non viene
        /// rieseguito: senza ricaricare qui, la griglia resterebbe con le righe del filtro
        /// precedente mentre l'intestazione mostra gia' quello nuovo.
        /// </summary>
        protected override async Task OnParametersSetAsync()
        {
            // Al primo render questo metodo gira dopo OnInitializedAsync, che ha gia' caricato:
            // il confronto con i filtri gia' applicati evita di rifare subito la stessa richiesta.
            if (_loadedTypeSearch == TypeSearch && _loadedIdUser == IdUser)
                return;

            // Prima del primo caricamento non c'e' ancora niente da aggiornare: ci pensa
            // OnInitializedAsync, che deve prima procurarsi utenti e aziende per i filtri.
            if (_loadedTypeSearch == null)
                return;

            await ApplyRouteFiltersAsync();
        }

        /// <summary>
        /// Traduce i parametri di rotta nei filtri della griglia, ricarica i dati e riallinea
        /// titolo e sottotitolo. Unico punto da cui passano sia il primo caricamento sia i
        /// successivi cambi di filtro.
        /// </summary>
        private async Task ApplyRouteFiltersAsync()
        {
            // Memorizzati prima di toccare IdUser: servono a riconoscere il prossimo cambio.
            _loadedTypeSearch = TypeSearch;
            _loadedIdUser = IdUser;

            if (TypeSearch == (int)TicketTypeSearch.Blocked)
            {
                IdUser = null;
                _idUser = null;
            }
            else if (IdUser != null)
            {
                _filterState = true;
                _idUser = IdUser;
            }

            await LoadData();

            _pageHeader = await HeaderService.Create(PageMode);
            ApplyHeaderFilters();
            StateHasChanged();
        }

        /// <summary>
        /// I parametri di rotta (stato e utente) sono filtri della lista, non risorse:
        /// vengono mostrati nel sottotitolo invece che nel breadcrumb.
        /// </summary>
        private void ApplyHeaderFilters()
        {
            if (_pageHeader == null)
                return;

            var filters = new List<string>();

            if (TypeSearch != (int)TicketTypeSearch.All)
                filters.Add($"{Localize["State"]}: {Localize[((TicketTypeSearch)TypeSearch).ToString()]}");

            if (!string.IsNullOrEmpty(_idUser))
            {
                var userName = _users.FirstOrDefault(u => u.Id == _idUser)?.NameComplete;
                if (!string.IsNullOrEmpty(userName))
                    filters.Add(userName);
            }

            if (filters.Any())
                _pageHeader.Subtitle = string.Join(" · ", filters);
        }

        private async Task LoadDataAllUser()
        {
            _idUser = null;
            await LoadData();
        }

        /// <summary>Cambia la natura del lavoro mostrata e ricarica dal server.</summary>
        private async Task SetCommessaFilter(bool? hasCommessa)
        {
            if (_hasCommessa == hasCommessa)
                return;

            _hasCommessa = hasCommessa;
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
                
                // Ordine di partenza: la scadenza piu' vicina in cima, cioe' cosa va fatto per
                // primo. Va imposto qui perche' al primo caricamento la griglia non manda ancora
                // alcun ordinamento, e il servizio ripiegherebbe sulla data del ticket.
                paging.OrderBy = "DateExpired asc";

                if (args != null)
                {
                    paging.Skip = args.Skip;
                    paging.Top = args.Top;

                    if (!string.IsNullOrWhiteSpace(args.OrderBy))
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
                paging.IdDeal = IdDeal;
                paging.IdCommessaFase = IdCommessaFase;
                paging.IdCommessa = IdCommessa;
                paging.HasCommessa = _hasCommessa;

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

        private async Task LoadCompanies()
        {
            var response = await _serviceCompany.GetListAsync(new CompanyFilter());
            _companies = response.ToList();
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
                if (IdCompany != null)
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
                    // L'esito va guardato: prima si ricaricava e basta, e un'eliminazione rifiutata
                    // era indistinguibile da una riuscita - il ticket restava li' senza un perche'.
                    var response = await _service.DeleteTicket(id.Value);

                    if (!response.Success)
                        NotificationService?.Notify(NotificationSeverity.Error, Localize["Elimina"],
                            response.ErrorMessage ?? Localize["Eliminazione non riuscita"]);

                    await LoadData();
                    StateHasChanged();
                }
            }
        }

        private async Task ClaimTicket(TicketDTO ticket)
        {
            if (ticket == null || _claimingTicketId != null)
                return;

            var claimed = false;

            try
            {
                _claimingTicketId = ticket.Id;
                var response = await _service.ClaimAsync(ticket.Id);
                if (!response.State)
                {
                    NotificationService?.Notify(NotificationSeverity.Error, "Presa in carico", response.Message ?? "Operazione non riuscita");
                    return;
                }

                NotificationService?.Notify(NotificationSeverity.Success, "Presa in carico", response.Message ?? "Ticket preso in carico");
                claimed = true;
            }
            finally
            {
                _claimingTicketId = null;

                // Se la presa in carico e' riuscita si lascia la lista: ridisegnarla sarebbe inutile.
                if (!claimed)
                    StateHasChanged();
            }

            // Chi prende in carico un ticket ci deve lavorare: si apre la sua scheda invece di
            // ricaricare la lista. Si passa da Details per rispettare le stesse rotte del
            // pulsante di visualizzazione (contesto azienda, filtro, lista dentro un'altra pagina).
            Details(ticket.Id);
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
                    var query = new List<string>();
                    if (IdDeal != null)
                        query.Add($"IdDeal={IdDeal}");

                    NavigationManager.NavigateTo($"/Tickets/New{(query.Any() ? "?" + string.Join("&", query) : string.Empty)}");
                }
            }
        }

        private string Header()
        {
            switch ((TicketTypeSearch)TypeSearch)
            {
                case TicketTypeSearch.Closed:
                    return Localize["Tickets Chiusi"];

                case TicketTypeSearch.Blocked:
                    return "Ticket bloccati";

                case TicketTypeSearch.ToClaim:
                    return Localize["Ticket da prendere in carico"];

            }
            return "Tickets";
        }

        private string DisplayState(TicketDTO? ticket)
        {
            var state = ticket?.State ?? eTicketStates.Created.ToString();

            // A questo stato ci arriva soltanto il cliente: dentro l'azienda "in lavorazione" non
            // esiste piu', un ticket assegnato e' un ticket su cui si lavora.
            if (state == eTicketStates.Processing.ToString())
                return Localize["In lavorazione"];

            return Localize[state];
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
            ApplyHeaderFilters();
            StateHasChanged();
        }

        private async Task OnChangeIdUser()
        {
            IdUser = _idUser;
            await LoadData();
            ApplyHeaderFilters();
            StateHasChanged();
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

        /// <summary>Dettaglio delle ore fatturabili per tipo, come tooltip della colonna ore.</summary>
        private string BillableDetail(TicketDTO? item)
        {
            if (item == null || item.MinuteBillable == 0)
                return string.Empty;

            var voci = new List<string>();
            if (item.MinuteWork > 0)
                voci.Add($"{Localize["Lavoro"]}: {DateTimeHelper.MinuteFormat(item.MinuteWork)}");
            if (item.MinuteTravel > 0)
                voci.Add($"{Localize["Viaggio"]}: {DateTimeHelper.MinuteFormat(item.MinuteTravel)}");
            if (item.MinuteBreak > 0)
                voci.Add($"{Localize["Pausa"]}: {DateTimeHelper.MinuteFormat(item.MinuteBreak)}");

            return string.Join(" · ", voci);
        }
    }
}
