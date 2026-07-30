using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
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
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Tickets
{
    [Authorize]
    public partial class Create : ComponentBase
    {
      
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private ITicketsService _service { get; set; }

        

        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> _usersService { get; set; }

        [Inject]
       
        private ICurrentUserService _userService { get; set; }

       
        [Inject]
        private ITicketTypesService _serviceTicketType { get; set; }

        
        [Inject]
        ICompaniesService CompaniesService { get; set; }

        [Inject]
        IProductsService ProductsService { get; set; }

        [Inject]
        IArticlesService ArticlesService { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        private NotificationService NotificationService { get; set; }

        [Inject]
        private IHeaderService HeaderService { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;


        private TicketCreateSteps _stepTicket = TicketCreateSteps.CompanyTicket;
        private Ticket _ticket = new Ticket() ;
        private List<ApplicationUser> _users = new List<ApplicationUser>();
        private ApplicationUser _user = new ApplicationUser();
        private List<CompanyDTO> _companies = new List<CompanyDTO>();
        private int _companyCount;
        private List<ProductDTO> _products = new List<ProductDTO>();
        private List<ArticleDTO> _articles = new List<ArticleDTO>();
        private List<TicketType> _ticketTypes = new List<TicketType>();
        private string _ragionaSociale;
        private TicketType _ticketType;
        private ProductDTO _product;
        private ArticleDTO _article;
        private ApplicationUser _userAssigned = new ApplicationUser();
        private string _messageCancel = "Annullare inserimento nuovo Ticket?";
        private string _messageError;

        private int _pageSize = 10;

       

        private bool _backDisabled = true;

        private PageHeaderModel? _pageHeader = null;

        // ─── Avanzamento della procedura ─────────────────────────────────────

        private sealed record StepInfo(TicketCreateSteps Step, string Label, string Title, string Icon);

        /// <summary>
        /// I passi effettivamente previsti per questo utente e per questo tipo di ticket: le stesse
        /// condizioni con cui NextStep salta i passi non pertinenti. Prima che il tipo sia scelto
        /// prodotto e data non sono ancora noti, quindi l'elenco si completa strada facendo.
        /// </summary>
        private List<StepInfo> _steps => BuildSteps();

        private List<StepInfo> BuildSteps()
        {
            var steps = new List<StepInfo>();

            if (_user.CanManageOtherCompany)
                steps.Add(new(TicketCreateSteps.CompanyTicket, Localize["Ditta"], Localize["Scelta della ditta"], "business"));

            steps.Add(new(TicketCreateSteps.TypeTicket, Localize["Tipo"], Localize["Scelta del tipo di ticket"], "category"));

            if (_ticketType != null && PropertyVisible(_ticketType.IdArticolo))
                steps.Add(new(TicketCreateSteps.ProductTicket, Localize["Prodotto"], Localize["Scelta del prodotto"], "inventory_2"));

            if (_ticketType != null && PropertyVisible(_ticketType.Date))
                steps.Add(new(TicketCreateSteps.DateTicket, Localize["Data"], Localize["Data e ora"], "event"));

            steps.Add(new(TicketCreateSteps.DescriptionTicket, Localize["Descrizione"], Localize["Descrizione del ticket"], "notes"));

            if (_user.CanTicketAssign)
                steps.Add(new(TicketCreateSteps.Assign, Localize["Assegnazione"], Localize["Assegna il ticket a un utente"], "person_add"));

            steps.Add(new(TicketCreateSteps.DataConfirm, Localize["Conferma"], Localize["Conferma dati"], "task_alt"));

            return steps;
        }

        /// <summary>Passi della procedura veri e propri: fuori restano esito e allegati.</summary>
        private bool IsWizardStep => _stepTicket <= TicketCreateSteps.DataConfirm;

        private int CurrentStepIndex => _steps.FindIndex(s => s.Step == _stepTicket);

        private StepInfo? CurrentStep => _steps.FirstOrDefault(s => s.Step == _stepTicket);

        private string CurrentStepTitle => CurrentStep?.Title ?? string.Empty;

        private string CurrentStepIcon => CurrentStep?.Icon ?? "edit";

        /// <summary>Il riepilogo compare da quando c'e' almeno un dato scelto.</summary>
        private bool HasRecap
            => _stepTicket > TicketCreateSteps.CompanyTicket
               && _stepTicket != TicketCreateSteps.Result
               && _stepTicket != TicketCreateSteps.Attachment;
        protected override async Task OnInitializedAsync()
        {
            try
            {
                _user = await _userService.Get();
                
               

                if (_user.CanManageOtherCompany)
                {
                    _stepTicket = TicketCreateSteps.CompanyTicket;

                    await Server();
                }
                else if (_user.IdCompany != null)
                {
                    await LoadCompanies();
                    _ticket.IdCompany = (int)_user.IdCompany;
                    SetCompany(_ticket.IdCompany);
                    await NextStep();
                }
                else
                {
                    Notify(Localize["Impossibile aprire un ticket: all'utente non è stata assegnato nessuna ditta."], NotificationSeverity.Error);
                }
                //_pageHeader = HeaderService.Create(ConstHelper.ClientTicketsPath, null, null, true, ConstHelper.ClientTicketsPath, null);
                _pageHeader = await HeaderService.Create(PageMode);
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
                    await LoadCompanies(new LoadDataArgs());
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

        public async Task LoadCompanies(LoadDataArgs args = null)
        {
            CompanyFilter request = new CompanyFilter();

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                request.RagioneSociale = args.Filter;
            }


            _companies = await CompaniesService.GetListAsync(request);
            

            
        }


        public async Task LoadProducts(LoadDataArgs args)
        {
            ProductFilter request = new ProductFilter();

            //var response = await _serviceProducts.Get(request);

            _products = await ProductsService.GetListAsync(request);

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

            _articles = await ArticlesService.GetListAsync(request); 

            

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
        
            var company = await CompaniesService.GetItemAsync(_ticket.IdCompany);
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
            NavigationManager.NavigateTo($"Tickets/{_ticket.Id}");
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
