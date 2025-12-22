using BlazoringComponents;
using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
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
        private ITicketService _service { get; set; }

        
        [Inject]
        IAGRestClientService RestClientService { get; set; }

        
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

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public int? IdArticle { get; set; }

        [Parameter]
        public int? IdProject { get; set; }

        [Parameter]
        public Action? OnClickSave { get; set; }

        [Parameter]
        public Action? OnClickCancel { get; set; }

        [Parameter]
        public DateTime? Date { get; set; }

        [Parameter]
        public bool Scheduler { get; set; } = true;

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        [Parameter]
        public string BackUrl { get; set; }


        private Ticket _ticket = null;

        private List<Company> _companies = new List<Company>();

        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private List<Product> _products = new List<Product>();

        private List<Article> _articles = new List<Article>();

        private List<TicketType> _ticketTypes = new List<TicketType>();

        private List<Project> _projects = new List<Project>();

        private bool _lockCompany = false;

        private bool _lockArticle = false;

        private string _header;

        private int _companyCount;
        private int _articleCount;

        private List<Contact> _contactsCustomer = new List<Contact>();

        private PropertyStates _dateTimeProperty;
        private PropertyStates _timeProperty;
        private PropertyStates _productProperty;
        private PropertyStates _articleProperty;
        private PropertyStates _dateEndProperty;

        private RadzenDropDownDataGrid<int> _ddCompany;
        private RadzenDropDownDataGrid<int?> _ddArticles;

        Dictionary<string, object> _inputTextAreaAttributes = new Dictionary<string, object>();

        private RadzenDropDownDataGrid<string> _ddUser;

        private RadzenDropDownDataGrid<int?> _ddContact;

        private bool _isLoading = false;


        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {
                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.TicketPath;


                await LoadCompany(new LoadDataArgs());
                await LoadProject(new LoadDataArgs());

                if (Id != null)
                {
                    path += $"/{Id}";
                    _header = Localize["Ticket Edit"];
                    _ticket = await _service.Get(Id.Value);
                }
                else
                {
                    _header = Localize["New Ticket"];
                    _ticket = new Ticket() { Date = Date ?? DateTime.Now, Time = null};
                   

                    if (IdCompany != null)
                        _ticket.IdCompany = IdCompany.Value;

                    if (IdArticle != null)
                        _ticket.IdArticle = IdArticle.Value;

                    if (IdProject != null)
                        _ticket.IdProject = IdProject.Value;

                }

                _lockCompany = IdCompany != null && IdCompany != 0;
                _lockArticle = IdArticle != null && IdArticle != 0;

                await LoadArticles();
                await LoadProducts();
                await LoadTicketType();
                await LoadUsers();
                await LoadContactsCustomer();

                _inputTextAreaAttributes.Add("rows", "20");

                OnChangeTicketType();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public async Task LoadCompany(LoadDataArgs args = null)
        {
            CompanyFilter request = new CompanyFilter() ; 

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                request.RagioneSociale = args.Filter;
            }

            var response = await RestClientService.GetListPag<CompanyFilter, Company>(request, ConstHelper.CompaniesPath);
            _companyCount = response.MetaData.TotalCount;
            _companies = response.Items.ToList();

            

        }

        private async Task LoadProject(LoadDataArgs args = null)
        {
            ProjectFilter request = new ProjectFilter();

            if (args != null && args.Filter != null)
                request.Filter = args.Filter;

            var resp = await RestClientService.GetListPag<ProjectFilter, Project>(request, ConstHelper.ProjectsPath);

            _projects = resp.Items; 
        }

        public async Task LoadProducts(LoadDataArgs args = null)
        {
            ProductFilter request = new ProductFilter();

           
            request.PageSize = 0;
            var response = await RestClientService.GetListPag<ProductFilter, Product>(request, ConstHelper.Products);  // await _serviceProducts.GetList(request);

            _products = response.Items.ToList();
            StateHasChanged();

        }

        private async Task LoadArticles(LoadDataArgs args = null)
        {
            ArticleFilter request = new ArticleFilter();

            request.IdProduct = _ticket.IdProduct;
            request.IdCompany = _ticket.IdCompany;

            var response = await RestClientService.GetListPag<ArticleFilter, Article>(request, ConstHelper.ArticlesPath);

            _articles = response?.Items?.ToList();
            _articleCount = response?.MetaData?.TotalCount ?? 0;

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
            var response = await RestClientService.GetListPag<ContactFilter, Contact>(request, ConstHelper.ContactsPath);

            _contactsCustomer = response.Items.ToList();

            StateHasChanged();
        }


        protected async Task HandleValidSubmit()
        {
            
            try
            {
                _isLoading = true;
                Waiting();
                if (Id == null)
                {
                    _ticket.DateOpened = DateTime.Now;
                    
                }
                _ticket = (await _service.Post(_ticket)).Data;
                
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
            await LoadProducts(new LoadDataArgs());
            await LoadArticles(new LoadDataArgs());
            await LoadContactsCustomer();
        }

        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                BackToUrl();
        }

       


        private void CompanyOnClickCancel()
        {
            dialogService.CloseSide();
        }

        private async Task OnGetCompany(int? id)
        {
            if (id != null)
            {
                await LoadCompany();
                await _ddCompany.SelectItem(id, true);
                
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

        private void OnGetScheduler(SchedulerUserDate? userDate)
        {
            if (userDate != null)
            {
                _ticket.Date = userDate.Date;

                if (userDate.IdUser != null)
                {
                    _ticket.IdUserAssigned = userDate.IdUser;
                    _ddUser.SelectItem(_ticket.IdUserAssigned, true);
                }
                
            }
        }

        private void DateOnChange(ChangeEventArgs args)
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
            if (BackUrl == null || BackUrl.Length == 0)
            {
                BackUrl = "/Tickets/Index";
            }
            else
                BackUrl = BackUrl.Replace("-", "/");

            NavigationManager.NavigateTo($"/Tickets/Index/{BackUrl}");
        }

        private void WaitingClose()
        {
            dialogService.Close();
        }

    }
}
