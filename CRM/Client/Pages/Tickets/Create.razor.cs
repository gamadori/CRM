using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Tickets
{
    [Authorize]
    public partial class Create : ComponentBase
    {
      
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private ITicketService _service { get; set; }


        

        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> _usersService { get; set; }

        [Inject]
       
        private IRestService<ApplicationUser> _userService { get; set; }

        [Inject]
        private AuthenticationStateProvider  _authenticationStateProvider { get; set; }

        
       

 //       [Inject]
        //private IBaseRestService<Product,ProductFilter, int> _serviceProducts { get; set; }

       
        [Inject]
        private ITicketTypesService _serviceTicketType { get; set; }

        
        [Inject]
        IAGRestClientService RestClientService { get; set; }


        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        private NotificationService NotificationService { get; set; }

        private TicketCreateSteps _stepTicket = TicketCreateSteps.CompanyTicket;
        private Ticket _ticket = new Ticket() ;
        private List<ApplicationUser> _users = new List<ApplicationUser>();
        private ApplicationUser _user = new ApplicationUser();
        private List<Company> _companies = new List<Company>();
        private int _companyCount;
        private List<Product> _products = new List<Product>();
        private List<Article> _articles = new List<Article>();
        private List<TicketType> _ticketTypes = new List<TicketType>();
        private string _ragionaSociale;
        private TicketType _ticketType;
        private Product _product;
        private Article _article;
        private ApplicationUser _userAssigned = new ApplicationUser();
        private string _messageCancel = "Annullare inserimento nuovo Ticket?";
        private string _messageError;

        private int _pageSize = 10;

        private DateTime _minTime;
        private DateTime _maxTime;

        private bool _backDisabled = true;
        /// <summary>
        /// Da Eliminare quando verranno inserite le traduzioni
        /// </summary>
        private static string[] _headerBuff = new string[] 
        {
            "Scelta della ditta",
            "Scelta del Tipo di ticket",
            "Scelta del prodotto",
            "Data e Ora",
            "Descrizione del Ticket", 
            "Data di Scadenza",
            "Assegna il ticked a un Utente",
            "Conferma Dati"
        };
        protected override async Task OnInitializedAsync()
        {
            try
            {
                _user = await _userService.Get();
                
                await TaskMinMaxTime();

                if (_user.CanManageOtherCompany)
                {
                    _stepTicket = TicketCreateSteps.CompanyTicket;

                    await Server();
                }
                else if (_user.IdCompany != null)
                {
                    await LoadCompany();
                    _ticket.IdCompany = (int)_user.IdCompany;
                    SetCompany(_ticket.IdCompany);
                    await NextStep();
                }
                else
                {
                    Notify(Localize["Impossibile aprire un ticket: all'utente non è stata assegnato nessuna ditta."], NotificationSeverity.Error);
                }

                
            }
            catch
            {

            }
        }


        protected async Task Server()
        {
            _ticket.Step = _stepTicket;

            switch (_stepTicket)
            {
                case TicketCreateSteps.CompanyTicket:
                    await LoadCompany(new LoadDataArgs());
                    _backDisabled = true;
                    break;

                case TicketCreateSteps.TypeTicket:
                    _backDisabled = !_user.CanManageOtherCompany;
                    await LoadTicketType(new LoadDataArgs());
                    break;
                case TicketCreateSteps.DateTicket:
                    _ticket.Date = DateTime.Now;
                    break;
                case TicketCreateSteps.ProductTicket:
                    _backDisabled = false;
                    await LoadProducts(new LoadDataArgs());
                    await LoadArticles(new LoadDataArgs());
                    break;

                case TicketCreateSteps.Assign:
                    _backDisabled = false;
                    await LoadUsers(new LoadDataArgs());
                    break;

                default:
                    _backDisabled = false;
                    break;
            }
            StateHasChanged();
        }
        protected async Task  NextStep()
        {
            switch (_stepTicket)
            {
                case TicketCreateSteps.CompanyTicket:
                    _stepTicket = TicketCreateSteps.TypeTicket;
                    break;

                case TicketCreateSteps.TypeTicket:
                    if (!PropertyVisible((PropertyStates)_ticketType.IdArticolo))
                    {
                        if (!PropertyVisible(_ticketType.Date))
                            _stepTicket = TicketCreateSteps.DescriptionTicket;
                        else
                            _stepTicket = TicketCreateSteps.DateTicket;
                    }
                    else
                        _stepTicket = TicketCreateSteps.ProductTicket;
                    break;

                case TicketCreateSteps.ProductTicket:
                    if (!PropertyVisible(_ticketType.Date))
                        _stepTicket = TicketCreateSteps.DescriptionTicket;
                    else
                        _stepTicket = TicketCreateSteps.DateTicket;
                    break;

                case TicketCreateSteps.DateTicket:
                    
                    _stepTicket = TicketCreateSteps.DescriptionTicket;
                    break;
                    
                case TicketCreateSteps.DescriptionTicket:
                    
                    if (_user.CanTicketAssign)
                        _stepTicket = TicketCreateSteps.Assign;
                    else
                        _stepTicket = TicketCreateSteps.DataConfirm;
                    break;

                case TicketCreateSteps.Assign:
                    _stepTicket = TicketCreateSteps.DataConfirm;
                    break;
                case TicketCreateSteps.DataConfirm:
                    _stepTicket = TicketCreateSteps.Result;
                    break;


            }
            await Server();
        }

        
        protected async Task PreviousStep()
        {
            switch (_stepTicket)
            {
                case TicketCreateSteps.TypeTicket:
                    _stepTicket = TicketCreateSteps.CompanyTicket;
                    break;

                case TicketCreateSteps.ProductTicket:
                    _stepTicket = TicketCreateSteps.TypeTicket;
                    break;

                case TicketCreateSteps.DateTicket:
                    if (!PropertyVisible(_ticketType.IdArticolo ))
                        _stepTicket = TicketCreateSteps.TypeTicket;
                    else
                        _stepTicket = TicketCreateSteps.ProductTicket;
                    break;

                case TicketCreateSteps.DescriptionTicket:
                    if (!PropertyVisible(_ticketType.Date))
                    {
                        if (!PropertyVisible(_ticketType.IdArticolo))
                            _stepTicket = TicketCreateSteps.TypeTicket;
                        else
                            _stepTicket = TicketCreateSteps.ProductTicket;
                    }
                    else
                        _stepTicket = TicketCreateSteps.DateTicket;
                    break;
                case TicketCreateSteps.Assign:
                    _stepTicket = TicketCreateSteps.DescriptionTicket;
                    break;

                case TicketCreateSteps.DataConfirm:
                    if (_user.CanTicketAssign)
                        _stepTicket = TicketCreateSteps.Assign;
                    else
                        _stepTicket = TicketCreateSteps.DescriptionTicket;
                    break;
            }
            await Server();
        }

        protected async void Submit()
        {
            _ticket.TicketType = null;
            var result = await _service.Post(_ticket);

            if (result != null)
            {
                _ticket = result.Data;
                await NextStep();


            }
            else
            {
                _messageError = "Si è verificato un errore durante il salvataggio del Ticket nel server";
                MsgBoxError();
            }
        }

        public async Task LoadCompany(LoadDataArgs args = null)
        {
            CompanyFilter request = new CompanyFilter();

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                request.RagioneSociale = args.Filter;
            }


            var response = await RestClientService.Get<Company, CompanyFilter>(request, ConstHelper.CompaniesPath);
            

            if (response != null)
            {
                _companyCount = response.MetaData.TotalCount;
                _companies = response.Items.ToList();
            }
        }


        public async Task LoadProducts(LoadDataArgs args)
        {
            ProductFilter request = new ProductFilter();

            //var response = await _serviceProducts.Get(request);

            var response = await RestClientService.Get<Product, ProductFilter>(request, ConstHelper.Products);

            _products = response.Items.ToList();

            StateHasChanged();

        }

        public async Task LoadArticles(LoadDataArgs args)
        {
            ArticleFilter request = new ArticleFilter();

            request.IdCompany = _ticket.IdCompany;

            if (_product != null)
            {
                request.IdProduct = _product.Id;
            }

            var response = await RestClientService.Get<Article, ArticleFilter>(request, ConstHelper.ArticlesPath); 

            _articles = response.Items.ToList();

            StateHasChanged();

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


        public async Task LoadUsers(LoadDataArgs args)
        {
            UsersFilterModel request = new UsersFilterModel();

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                request.NameComplete = args.Filter;
            }
            var response = await _usersService.Get(request);

            _users = response.Items;

            StateHasChanged();
        }

        protected async void Cancel()
        {
            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgCancel");
            NavigationManager.NavigateTo("/");
        }

        protected async void Close()
        {
            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgError");
        }

        protected async void OnChangeCompany(object value)
        {
            
            SetCompany((int)value);

            await LoadArticles(new LoadDataArgs());
        }

        protected async Task<bool> GetCompany()
        {
        
            var company = await RestClientService.GetItem<Company, int>(_ticket.IdCompany, ConstHelper.CompaniesPath);
            _ragionaSociale = company?.RagioneSociale;

            return company != null;
        }
        protected  void OnChangeTicketType()
        {
            _ticketType = _ticketTypes.Where(x => x.Id == _ticket.IdType).FirstOrDefault();

            if (_ticket != null)
                _ticket.TicketType = _ticketType;

            StateHasChanged();
        }

        protected async void OnChangeIdProduct()
        {
            _product = _products.Where(x => x.Id == _ticket.IdProduct).FirstOrDefault();
            await LoadArticles(new LoadDataArgs());
            StateHasChanged();
        }

        protected void OnChangeIdArticle()
        {
            _article = _articles.Where(x => x.Id == _ticket.IdArticle).FirstOrDefault();

            StateHasChanged();
        }

        protected void OnChangeIdUser()
        {
            _userAssigned = _users.Where(x => x.Id == _ticket.IdUserAssigned).FirstOrDefault();

            StateHasChanged();
        }

        protected async void OnSelectCompany(int idCompany)
        {
            _ticket.IdCompany = idCompany;
            await NextStep();
        }
        protected void PrepareCancel()
        {
            JSRuntime.InvokeVoidAsync("ShowModal", "dlgCancel");
        }

        protected void MsgBoxError()
        {
            JSRuntime.InvokeVoidAsync("ShowModal", "dlgError");
        }

        private async Task TaskMinMaxTime()
        {
            GlobalSetting _settings = await RestClientService.GetFirst<GlobalSetting>(ConstHelper.GlobalSettingsPath);

            if (_settings != null)
            {
                _minTime = DateTime.Today + _settings.ScheduleTimeStart.TimeOfDay;
                _maxTime = DateTime.Today + _settings.ScheduleTimeEnd.TimeOfDay;
            }
        }

        private void SetCompany(int idCompany)
        {
            var company = _companies.Where(x => x.Id == idCompany).FirstOrDefault();

            if (company != null)
                _ragionaSociale = company.RagioneSociale;
        }
        private void Notify(string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity };
            NotificationService?.Notify(message);
        }

        private async void OpenAttachmentPage()
        {
            _stepTicket = TicketCreateSteps.Attachment;
            await Server();
        }

        private void ListaTickets()
        {
            NavigationManager.NavigateTo("Tickets/Index");
        }
        private void OpenTicket()
        {
            NavigationManager.NavigateTo($"Tickets/Info/{_ticket.Id}");
        }

        private bool PropertyVisible(PropertyStates? property)
        {
            
           
            if (property == PropertyStates.OptionalAdmin || property == PropertyStates.RequiredAdmin)
                return !_user.IsClient;
            else
                return property == PropertyStates.Optional || property == PropertyStates.Required;
        }

        private bool PropertyVisible(int p)
        {
            return PropertyVisible((PropertyStates)p);
        }
       
    }
}
