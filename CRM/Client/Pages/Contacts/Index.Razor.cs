using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BlazoringComponents;
using Radzen;
using Radzen.Blazor;
using Microsoft.Extensions.Localization;
using CRM.Shared.Helper;
using static CRM.Client.Helpers.PageHelper;
using CRM.Client.Models;

namespace CRM.Client.Pages.Contacts
{
    [Authorize]
    public partial class Index: ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }

       

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject] 
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int? IdContact { get; set; }

      
        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }
        
        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public EventCallback OnClickNew { get; set; }

        [Parameter]
        public EventCallback<int?> OnClickSelect { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        [Parameter]
        public bool CmdDetails { get; set; } = true;

        [Parameter]
        public bool CmdEdit { get; set; } = true;

        [Parameter]
        public bool CmdDelete { get; set; } = true;

        [Parameter]
        public int? IdCompany { get; set; }

        

        private List<Contact> _contacts = null;


        private List<CompanyFilter> _companies;


        private PagingHeaderModel _paging = new PagingHeaderModel();

        private ContactFilter _filter = new ContactFilter() { PageSize = ConstHelper.PageSize, Skip = 0, Top = ConstHelper.PageSize };

        private string _messageDelete = "";

        private Contact _contact;

        private int _companiesCount = 0;

        private int _contactsCount = 0;

        private string pagingSummaryFormat;

        private bool _isLoading = false;

        private string _search = string.Empty;

        private FilterMode _filterMode = FilterMode.Advanced;

        private RadzenDataGrid<Contact> grdContacts;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
     

            // navMenuService.CallRequestRefresh();
            _isLoading = true;
            await LoadCompany(new LoadDataArgs() { Skip = 0, Top = ConstHelper.PageSize });
            

            pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];

            if (PageMode == PageModality.Dialog)
                _filterMode = FilterMode.SimpleWithMenu;

            await LoadData();

            //_pageHeader = HeaderService.Create(ConstHelper.ClientContactsPath);
            _pageHeader = await HeaderService.Create(PageMode);
        }

        public async Task LoadData(LoadDataArgs args = null)
        {

            _isLoading = true;

            _contacts = Enumerable.Empty<Contact>().ToList();
            try
            {
                
                
                if (IdCompany != null)
                    _filter.IdCompany = IdCompany;

                if (_search.Length > 0)
                {
                    _filter.Name = _search;
                }
                else
                    _filter.Name = null;

                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;
                    _filter.Filter = args?.Filter;
                    _filter.OrderBy = args?.OrderBy;
                }
                var pagingResponse = await RestClientService.Get<Contact, ContactFilter>(_filter, ConstHelper.ContactsPath);

                _contacts = pagingResponse.Items;
                _paging = pagingResponse.MetaData;

                
               
                
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                
            }
            finally
            {
                _isLoading = false;

                await InvokeAsync(StateHasChanged);
            }
     
        }

       
        public async Task LoadCompany(LoadDataArgs args)
        {
            CompanyFilter request = new CompanyFilter();

            request.PageSize = 0;
            if (args != null) 
            {
                request.RagioneSociale = args.Filter;
                request.Skip = args.Skip;
                request.Top = args.Top;
            }
            var response = await RestClientService.Get<Company, CompanyFilter>(request, ConstHelper.CompaniesPath);

            _companies = response.Items.Select(x => new CompanyFilter() { Id = x.Id, RagioneSociale = x.RagioneSociale }).ToList();
            _companiesCount = response.MetaData.TotalCount;

            await InvokeAsync(StateHasChanged);
        }

       

        protected void Details(int id)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(id);
            }
            else
                NavigationManager.NavigateTo($"/Contacts/{id}");
        }

        

        protected void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/Contacts/{id}/Edit");
        }
        protected void Cancel()
        {
            NavigationManager.NavigateTo($"/Contacts");
        }
        protected async void NewItem()
        {
            if (OnClickNew.HasDelegate)
                await OnClickNew.InvokeAsync();
            else
                NavigationManager.NavigateTo($"/Contacts/New");
        }

        protected async Task Delete(Contact contact)
        {
        
            if (await DialogService.Confirm($"{Localize["Delete the Contact"]} {contact.NameComplete}") == true)
            {
                if (OnClickDelete != null)
                    OnClickDelete(contact.Id);
                else
                {
                    await RestClientService.Delete<int>(contact.Id, ConstHelper.ContactsPath);
                    await LoadData();
                }
            }
        
        }

       
        
        protected async Task PageChanged(Radzen.PagerEventArgs args)
        {
            _filter.PageNumber = args.PageIndex + 1;
            await LoadData();
            StateHasChanged();

        }

        protected void ImportData()
        {
            NavigationManager.NavigateTo($"/CSVSettings/CSVData/{CSVTable.Article.ToString()}");
        }

        private async Task OnClickName(int id)
        {
            switch (PageMode)
            {
                case PageModality.Dialog:

                    if (OnClickSelect.HasDelegate)
                    {
                        await OnClickSelect.InvokeAsync(id);
                    }
                    else
                        DialogService.CloseSide(id);
                    break;
                case PageModality.Visualization:
                    Details(id);
                    break;
            }

        }

        protected async Task OnChangeNameFilter(ChangeEventArgs args)
        {
            _search = args.Value.ToString();
            await grdContacts.GoToPage(0);
            await LoadData();
        }
    }
}
