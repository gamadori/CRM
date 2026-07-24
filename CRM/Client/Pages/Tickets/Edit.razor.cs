using BlazoringComponents;
using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Tickets
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
        
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private ITicketsService _service { get; set; }

        
        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        ICompaniesService CompaniesService { get; set; }

        [Inject]
        IProductsService ProductsService { get; set; }

        [Inject]
        IArticlesService ArticlesService { get; set; }

        [Inject]
        IContactsService ContactsService { get; set; }

        [Inject]
        IDealService DealService { get; set; }

        [Inject]
        private ITicketTypesService _serviceTicketType { get; set; }
        

        [Inject]
        IBaseRestService<ApplicationUser, UsersFilterModel, string> _serviceUser { get; set; }


        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService dialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        [SupplyParameterFromQuery]
        public int? IdCompany { get; set; }

        [Parameter]
        [SupplyParameterFromQuery]
        public int? IdArticle { get; set; }

        [Parameter]
        [SupplyParameterFromQuery]
        public int? IdDeal { get; set; }

        // Descrizione precompilata (es. da "prendi in carico" fase di commessa)
        [Parameter]
        [SupplyParameterFromQuery]
        public string? Descr { get; set; }

        // Precompilazione da "prendi in carico" di una fase di commessa
        [Parameter]
        [SupplyParameterFromQuery]
        public int? IdCommessaFase { get; set; }

        [Parameter]
        [SupplyParameterFromQuery]
        public int? IdType { get; set; }

        [Parameter]
        [SupplyParameterFromQuery]
        public int? IdGroup { get; set; }

        [Parameter]
        public Action? OnClickSave { get; set; }

        [Parameter]
        public Action? OnClickCancel { get; set; }

        [Parameter]
        public DateTime? Date { get; set; }

        [Parameter]
        public bool ShowPlanningPicker { get; set; } = true;

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        [Parameter]
        public string BackUrl { get; set; }


        private Ticket _ticket = null;

        private List<CompanyDTO> _companies = new List<CompanyDTO>();

        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private List<ProductDTO> _products = new List<ProductDTO>();

        private List<ArticleDTO> _articles = new List<ArticleDTO>();

        private List<ContactDTO> _contactsCustomer = new List<ContactDTO>();

        private List<TicketType> _ticketTypes = new List<TicketType>();

        private List<DealDTO> _deals = new List<DealDTO>();

        private bool _lockCompany = false;

        private bool _lockArticle = false;

        private string _header;

        private PropertyStates _dateTimeProperty;
        private PropertyStates _timeProperty;
        private PropertyStates _productProperty;
        private PropertyStates _articleProperty;
        private PropertyStates _dateEndProperty;

        private RadzenDropDownDataGrid<int> _ddCompany;
        private RadzenDropDownDataGrid<int?> _ddArticles;

        Dictionary<string, object> _inputTextAreaAttributes = new Dictionary<string, object>();

        private RadzenDropDownDataGrid<string> _ddUser;

        private bool _isLoading = false;

        private PageHeaderModel? _pageHeader = null;

        private GlobalSetting? _globalSettings = null;

       

        // ✅ NUOVO: Multi-user assignment
        private HashSet<string> _selectedUserIds = new HashSet<string>();
        private List<ApplicationUser> _filteredUsers = new List<ApplicationUser>();
        private string _searchQuery = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {
                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.TicketPath;

                // Carica le impostazioni globali per validazione orario
                await LoadGlobalSettings();

                await LoadCompany(new LoadDataArgs());

                if (Id != null)
                {
                    path += $"/{Id}";
                    _header = Localize["Ticket Edit"];
                    _ticket = await _service.Get(Id.Value);
                    // ✅ NUOVO: Carica gli utenti già assegnati al ticket
                    await LoadAssignedUsers();
                }
                else
                {
                    _header = Localize["New Ticket"];
                    _ticket = new Ticket() { Date = Date ?? DateTime.Now, Time = null};
                   

                    if (IdCompany != null)
                        _ticket.IdCompany = IdCompany.Value;

                    if (IdArticle != null)
                        _ticket.IdArticle = IdArticle.Value;

                    if (IdDeal != null)
                        _ticket.IdDeal = IdDeal.Value;

                    if (IdCommessaFase != null)
                        _ticket.IdCommessaFase = IdCommessaFase.Value;

                    if (IdType != null)
                        _ticket.IdType = IdType.Value;

                    if (IdGroup != null)
                        _ticket.IdGroupAssigned = IdGroup.Value;

                    if (!string.IsNullOrWhiteSpace(Descr))
                        _ticket.Description = Descr;

                    await ApplyLinkedContextAsync();

                }

                _lockCompany = IdCompany != null && IdCompany != 0;
                _lockArticle = IdArticle != null && IdArticle != 0;

                await LoadArticles();
                await LoadProducts();
                await LoadTicketType();
                await LoadUsers();
                await LoadContactsCustomer();
                await LoadDeals();

                _inputTextAreaAttributes.Add("rows", "20");

                //_pageHeader = HeaderService.Create("Tickets", Id, _ticket?.Numero, true, ConstHelper.ClientTicketsPath, null, PageMode);
                _pageHeader = await HeaderService.Create(PageMode);

                OnChangeTicketType();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// ✅ NUOVO: Carica gli utenti già assegnati al ticket
        /// </summary>
        private async Task LoadAssignedUsers()
        {
            try
            {
                if (_ticket == null || _ticket.Id == 0)
                    return;

                var response = await HttpClient.GetFromJsonAsync<List<string>>($"api/Tickets/{_ticket.Id}/assigned-users");

                if (response != null && response.Any())
                {
                    _selectedUserIds = new HashSet<string>(response);
                }
                else if (!string.IsNullOrEmpty(_ticket.IdUserAssigned))
                {
                    // Fallback per retrocompatibilità
                    _selectedUserIds.Add(_ticket.IdUserAssigned);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti assegnati: {ex.Message}");
                
                // Fallback su utente legacy
                if (!string.IsNullOrEmpty(_ticket.IdUserAssigned))
                {
                    _selectedUserIds.Add(_ticket.IdUserAssigned);
                }
            }
        }

        private async Task LoadGlobalSettings()
        {
            try
            {
                _globalSettings = await RestClientService.GetFirst<GlobalSetting>(ConstHelper.GlobalSettingsPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento GlobalSettings: {ex.Message}");
            }
        }

        public async Task LoadCompany(LoadDataArgs args = null)
        {
            CompanyFilter request = new CompanyFilter() ; 

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                request.RagioneSociale = args.Filter;
            }

            _companies = await CompaniesService.GetListAsync(request);
            
        }

        private async Task LoadDeals(LoadDataArgs args = null)
        {
            var request = new DealFilter();

            if (args != null && !string.IsNullOrWhiteSpace(args.Filter))
                request.Search = args.Filter;

            _deals = await DealService.GetListAsync(request) ?? new List<DealDTO>();
        }

        private async Task ApplyLinkedContextAsync()
        {
            if (_ticket.IdDeal != null && _ticket.IdCompany <= 0)
            {
                var deal = await DealService.GetItemAsync(_ticket.IdDeal.Value);

                if (deal?.IdCompany != null)
                    _ticket.IdCompany = deal.IdCompany.Value;

                if (deal?.IdContact != null && _ticket.IdContact == null)
                    _ticket.IdContact = deal.IdContact;
            }
        }

        public async Task LoadProducts(LoadDataArgs args = null)
        {
            ProductFilter request = new ProductFilter();
           
            _products = await ProductsService.GetListAsync(request);  // await _serviceProducts.GetList(request);

            StateHasChanged();

        }

        private async Task LoadArticles(LoadDataArgs args = null)
        {
            ArticleFilter request = new ArticleFilter();

            request.IdProduct = _ticket.IdProduct;
            request.IdCompany = _ticket.IdCompany;

            var response = await ArticlesService.GetListAsync(request);

            _articles = response;
            
            StateHasChanged();
        }

        private async Task ProductChange()
        {
            await LoadArticles();
        }
        public async Task LoadTicketType(LoadDataArgs args = null)
        {
            TicketTypeFilter request = new TicketTypeFilter();

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                request.Desc = args.Filter;
            }
            var response = await _serviceTicketType.GetList(request);

            _ticketTypes = response.Items;

            StateHasChanged();
        }

        private async Task LoadUsers(LoadDataArgs args = null)
        {
            UsersFilterModel request = new UsersFilterModel();

            if (args != null)
            {
                request.Filter = args.Filter;
                request.OrderBy = args.OrderBy;
            }

            request.IdTicketToAssign = _ticket?.Id;
            request.TicketTypeToAssign = _ticket?.IdType;

            var response = await _serviceUser.Get(request);

            _users = response.Items.ToList();
            
            // ✅ FIX: Inizializza lista filtrata escludendo utenti già selezionati
            _filteredUsers = _users
                .Where(u => !_selectedUserIds.Contains(u.Id))
                .ToList();
    
            StateHasChanged();
      
        }

        private async Task LoadContactsCustomer(LoadDataArgs args = null)
        {
            ContactFilter request = new ContactFilter();

            if (args != null)
            {
                request.Filter = args.Filter;
                request.OrderBy = args.OrderBy;
            }


            request.IdCompany = _ticket?.IdCompany;
            _contactsCustomer = await ContactsService.GetListAsync(request);

           

            StateHasChanged();
        }

        /// <summary>
        /// ✅ NUOVO: Gestisce la ricerca utenti in tempo reale
        /// </summary>
        private void OnSearchChanged(string searchQuery)
        {
            _searchQuery = searchQuery?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                // ✅ FIX: Escludi utenti già selezionati dalla lista disponibile
                _filteredUsers = _users
                    .Where(u => !_selectedUserIds.Contains(u.Id))
                    .ToList();
            }
            else
            {
                // ✅ FIX: Escludi utenti già selezionati + applica filtro ricerca
                _filteredUsers = _users
                    .Where(u => !_selectedUserIds.Contains(u.Id) &&
                               (u.NameComplete.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                u.Email.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            StateHasChanged();
        }

        /// <summary>
        /// ✅ NUOVO: Toggle selezione utente
        /// </summary>
        private void ToggleUser(string userId)
        {
            if (_selectedUserIds.Contains(userId))
            {
                _selectedUserIds.Remove(userId);
            }
            else
            {
                _selectedUserIds.Add(userId);
            }

            // ✅ FIX: Aggiorna la lista filtrata per rimuovere/aggiungere l'utente
            OnSearchChanged(_searchQuery);
        }

        /// <summary>
        /// ✅ NUOVO: Rimuove un utente dalla selezione
        /// </summary>
        private void RemoveUser(string userId)
        {
            _selectedUserIds.Remove(userId);
            
            // ✅ FIX: Aggiorna la lista filtrata per mostrare di nuovo l'utente rimosso
            OnSearchChanged(_searchQuery);
        }

        /// <summary>
        /// ✅ NUOVO: Ottieni iniziali dal nome completo
        /// </summary>
        private string GetInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "?";

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "?";

            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();

            return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
        }

        protected async Task HandleValidSubmit()
        {
            
            try
            {
                // ⚠️ Validazione orario prima di salvare
                if (!ValidateScheduleTime())
                {
                    return; // Blocca il salvataggio se l'orario non è valido
                }

                _isLoading = true;
                Waiting();
                if (Id == null)
                {
                    _ticket.DateOpened = DateTime.Now;
                    
                }

                // ✅ NUOVO: Sincronizza IdUserAssigned con il primo utente selezionato (retrocompatibilità)
                if (_selectedUserIds.Any())
                {
                    _ticket.IdUserAssigned = _selectedUserIds.First();
                }
                else
                {
                    _ticket.IdUserAssigned = null;
                }

                var response = await _service.Post(_ticket);
                
                // ✅ FIX: Controllo null sul risultato del POST
                if (response?.State != true)
                {
                    NotificationService.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Error,
                        Summary = Localize["Error"],
                        Detail = Localize["Failed to save ticket"],
                        Duration = 4000
                    });
                    return;
                }
                else if (response?.Data != null)
                    _ticket = response.Data;

                // ✅ NUOVO: Salva le assegnazioni multiple tramite API
                if (_ticket?.Id > 0)
                {
                    await SaveMultipleAssignments(_ticket.Id);
                }
                
                if (OnClickSave != null)
                    OnClickSave();
                else
                    BackToUrl();
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }

            finally
            {
                _isLoading = false;
                WaitingClose();
            }
        }

        /// <summary>
        /// ✅ NUOVO: Salva le assegnazioni multiple degli utenti al ticket
        /// </summary>
        private async Task SaveMultipleAssignments(int ticketId)
        {
            try
            {
                var assignmentData = new
                {
                    ticketId = ticketId,
                    userIds = _selectedUserIds.ToList()
                };

                var response = await HttpClient.PostAsJsonAsync($"api/Tickets/{ticketId}/assign-users", assignmentData);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Errore assegnazione utenti: {errorMsg}");
                    
                    NotificationService.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Warning,
                        Summary = Localize["Warning"],
                        Detail = Localize["Failed to assign users"],
                        Duration = 4000
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore salvataggio assegnazioni: {ex.Message}");
            }
        }

        /// <summary>
        /// Valida che l'orario inserito nel ticket rientri nell'intervallo definito in GlobalSettings
        /// </summary>
        /// <returns>True se valido o se non ci sono vincoli, False se fuori range</returns>
        private bool ValidateScheduleTime()
        {
            // Se non c'è orario inserito, non serve validare
            if (_ticket?.Time == null)
                return true;

            // Se non ci sono impostazioni globali o orari non configurati, permetti tutto
            if (_globalSettings == null || 
                !_globalSettings.ScheduleTimeStart.HasValue || 
                !_globalSettings.ScheduleTimeEnd.HasValue)
                return true;

            var ticketTime = _ticket.Time.Value;
            var scheduleStart = _globalSettings.ScheduleTimeStart.Value;
            var scheduleEnd = _globalSettings.ScheduleTimeEnd.Value;

            // Controlla se l'orario è fuori range
            if (ticketTime < scheduleStart || ticketTime > scheduleEnd)
            {
                // Mostra notifica warning
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Warning,
                    Summary = "⚠️ Orario fuori dall'intervallo consentito",
                    Detail = $"L'orario inserito ({ticketTime:HH:mm}) è fuori dall'intervallo configurato ({scheduleStart:HH:mm} - {scheduleEnd:HH:mm}). " +
                             "Verifica le impostazioni o modifica l'orario del ticket.",
                    Duration = 8000 // 8 secondi
                });

                return false; // Blocca il salvataggio
            }

            return true; // Orario valido
        }

        
        protected async void OnChangeTicketType()
        {
            var ticketType = await _serviceTicketType.Get(_ticket.IdType);

            if (ticketType != null)
            {
                _dateTimeProperty = (PropertyStates) ticketType.Date;
                _timeProperty = (PropertyStates)ticketType.Time;
                _productProperty = (PropertyStates)ticketType.IdProdotto;
                _articleProperty = (PropertyStates)ticketType.IdArticolo;
                _dateEndProperty = (PropertyStates)ticketType.DateEnd;
            }

           await LoadUsers();


            StateHasChanged();
        }    

        
        protected async void OnChangeCompany(object value)
        {
            await CompanyChangedAsync();
        }

        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                BackToUrl();
        }

       
        private async Task CompanyChangedAsync()
        {
            await LoadProducts(new LoadDataArgs());
            await LoadArticles(new LoadDataArgs());
            await LoadContactsCustomer();
        }

        private Task DealChangedAsync() => Task.CompletedTask;

        private void CompanyOnClickCancel()
        {
            dialogService.CloseSide();
        }

        private async Task OnGetCompany(object? id)
        {
            if (id != null)
            {
                await LoadCompany();
                StateHasChanged();
                _ticket.IdCompany = (int)id;
                await CompanyChangedAsync();
                StateHasChanged();

            }
        }

        private async Task OnGetArticle(int? id)
        {
            if (id != null)
            {
                await LoadArticles();
                await _ddArticles.SelectItem(id, true);

            }
        }

        private void ApplyPlanningSelection(TicketPlanningSelection? selection)
        {
            if (selection != null)
            {
                _ticket.Date = selection.Date;

                if (selection.IdUser != null)
                {
                    // ✅ NUOVO: Aggiungi utente alla selezione multipla invece di sostituire
                    if (!_selectedUserIds.Contains(selection.IdUser))
                    {
                        _selectedUserIds.Add(selection.IdUser);
                    }
                    
                    // Mantieni retrocompatibilità
                    _ticket.IdUserAssigned = selection.IdUser;
                    _ddUser?.SelectItem(_ticket.IdUserAssigned, true);
                }
                
            }
        }

        private void DateOnChange(DateTime? args)
        {
            if (_ticket.DateEnd < _ticket.Date)
                _ticket.DateEnd = _ticket.Date;
        }
        private void Waiting()
        {
            dialogService.Open<WaitingSpinner>("", new Dictionary<string, object>() { { "Header", "Attendi Completamento Operazione" } },
                new DialogOptions() { ShowTitle = false, Style = "min-height:auto;min-width:auto;width:auto", CloseDialogOnEsc = false });
        }

        private void BackToUrl()
        {
           
            if (BackUrl != null && BackUrl.Any())
                BackUrl = BackUrl.Replace("-", "/");

            NavigationManager.NavigateTo($"/Tickets/Index/{BackUrl}");
        }

        private void WaitingClose()
        {
            dialogService.Close();
        }

        /// <summary>
        /// ✅ AGGIORNATO: Apre il dialog di assegnazione utenti usando Assign.razor
        /// </summary>
        private async Task OpenUserSelectionDialog()
        {
            try
            {
                var parameters = new Dictionary<string, object>();
                
                // ✅ FIX: Passa parametri diversi per nuovo ticket vs ticket esistente
                if (_ticket?.Id > 0)
                {
                    // Ticket esistente: passa solo l'Id
                    parameters.Add("TicketTypeId", _ticket.IdType);
                    parameters.Add("Id", _ticket.Id);
                }
                else
                {
                    // Nuovo ticket: passa i dati del ticket e gli utenti preselezionati
                    parameters.Add("Id", 0);
                    parameters.Add("TicketTypeId", _ticket.IdType);
                    
                   
                }
                parameters.Add("PreselectedUserIds", _selectedUserIds);
                parameters.Add("Date", _ticket.Date);

                var options = new DialogOptions
                {
                    Width = "900px",
                    Height = "80vh",
                    Resizable = true,
                    Draggable = true,
                    CloseDialogOnEsc = true
                };

                var result = await dialogService.OpenAsync<Assign>(
                    Localize["Assign Users"], 
                    parameters, 
                    options
                );

                // ✅ FIX: Gestisci risultato diverso per nuovo ticket vs ticket esistente
                if (result != null)
                {
                    if (_ticket?.Id > 0)
                    {
                        // Ticket esistente: ricarica dal server
                        if (result.Equals(true))
                        {
                            await LoadAssignedUsers();
                            StateHasChanged();
                        }
                    }
                    else
                    {
                        // Nuovo ticket: aggiorna la selezione locale
                        if (result is HashSet<string> selectedUsers)
                        {
                            _selectedUserIds = selectedUsers;
                            
                            // Aggiorna la lista filtrata
                            _filteredUsers = _users
                                .Where(u => !_selectedUserIds.Contains(u.Id))
                                .ToList();
                            

                            StateHasChanged();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore apertura dialog assegnazione utenti: {ex.Message}");
            }
        }

        private async Task OnGetContact(object? id)
        {
            if (id != null)
            {
                await LoadContactsCustomer();
                StateHasChanged();
                _ticket.IdContact = (int)id;
                StateHasChanged();

            }
        }

    }
}
