using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CRM.Client.Pages.Articles.Info;

namespace CRM.Client.Pages.Companies
{
    public partial class Info: ComponentBase
    {
        

        public enum PartialViews
        {
            Index,
            Details,
            Edit,
            New

        }

        [Inject]
        ICompaniesService companiesService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }   

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public int? SelectViewValue { get; set; }

        

        protected CompanyViews _selectView = CompanyViews.Company;

        private PartialViews _partialView = PartialViews.Details;

        private string _idUser;

        private int? _idProduct;

        private int? _idTicket;

        private int? _idContact;

        private int? _idContract;

        private int? _idCustomer;

        private Company _company = null;

        private PageHeaderModel _pageHeader;

        private bool _fromDetails = false;

        private List<ViewOption<CompanyViews>> _viewOptions;

        protected override async Task OnInitializedAsync()
        {
            if (SelectViewValue != null)
            {
                _selectView = (CompanyViews)SelectViewValue;
                _partialView = PartialViews.Index;
            }
            await LoadCompany();
            InitializeViewOptions();
            _pageHeader  = await HeaderService.Create();  

        }
        private void InitializeViewOptions()
        {
            _viewOptions = new List<ViewOption<CompanyViews>>
            {
                new ViewOption<CompanyViews> { Text = Localize["Dati Azienda"], Value = CompanyViews.Company },
                new ViewOption<CompanyViews> { Text = Localize["Utenti"], Value = CompanyViews.Users },
                new ViewOption<CompanyViews> { Text = Localize["Contacts"], Value = CompanyViews.Contacts },
                new ViewOption<CompanyViews> { Text = Localize["Articles"], Value = CompanyViews.Articles },
                new ViewOption<CompanyViews> { Text = Localize["Tickets"], Value = CompanyViews.Ticket },
            };
        }
        private async Task LoadCompany()
        {
            _company = await companiesService.GetItem<Company, int>(Id, ConstHelper.CompaniesPath);
        }
        private void EditCompany()
        {
            _selectView = CompanyViews.Company;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }


        void CancelCompany()
        {
            _selectView = CompanyViews.Company;
            _partialView = PartialViews.Details;

            StateHasChanged();
        }


        private async Task SaveCompany()
        {
            _selectView = CompanyViews.Company;
            _partialView = PartialViews.Details;
            await LoadCompany();
            StateHasChanged();
        }
        private void EditUser(string id)
        {
            _selectView = CompanyViews.Users;
            _partialView = PartialViews.Edit;
            _idUser = id;

            StateHasChanged();
        }

        private void IndexEditUser(string id)
        {
            _fromDetails = false;
            EditUser(id);
        }

        private void DetailsEditUser(string id)
        {
            _fromDetails = true;
            EditUser(id);
        }
        
        private void DetailsUser(string id)
        {
            _selectView = CompanyViews.Users;
            _partialView = PartialViews.Details;
            _idUser = id;

            StateHasChanged();
        }

        private void UsersCloseForm()
        {
            _selectView = CompanyViews.Users;

            if (_fromDetails)
                _partialView = PartialViews.Details;
            else
                _partialView = PartialViews.Index;

            _fromDetails = false;
            StateHasChanged();
        }

        void Change(object value, string name)
        {
            if (_selectView == CompanyViews.Company)
                _partialView = PartialViews.Details;
            else
            {
                _fromDetails = false;
                _partialView = PartialViews.Index;
            }
            StateHasChanged();
        }

        #region Products
        private void EditProduct(int? id)
        {
            _selectView = CompanyViews.Articles;
            _partialView = PartialViews.Edit;
            _idProduct = id;

            StateHasChanged();
        }

        private void IndexEditProduct(int? id)
        {
            _fromDetails = false;
            EditProduct(id);
        }

        private void DetailsProduct(int id)
        {
            NavigationManager.NavigateTo($"/Companies/{Id}/Articles/{id}");

            /*selectView = CompanyViews.Products;
            _partialView = PartialViews.Details;
            _idProduct = id;

            StateHasChanged();*/
        }

        private void DetailsEditProduct()
        {
            _fromDetails = true;
            EditProduct(_idProduct);
        }

        private void ProductCloseForm()
        {
            _selectView = CompanyViews.Articles;

            if (_fromDetails)
                _partialView = PartialViews.Details;
            else
                _partialView = PartialViews.Index;

            _fromDetails = false;
            StateHasChanged();
        }


        #endregion

        #region Tickets

        private void IndexTicket()
        {
            _selectView = CompanyViews.Ticket;
            _partialView = PartialViews.Index;

            StateHasChanged();
        }
        private void EditTicket(int? id)
        {
            _selectView = CompanyViews.Ticket;
            _partialView = PartialViews.Edit;
            _idTicket = id;

            StateHasChanged();
        }

        private void IndexEditTicket(int? id)
        {
            _fromDetails = false;
            EditTicket(id);
        }

      

        private void DetailsTicket(int id)
        {
        //    _selectView = CompanyViews.Ticket;
        //    _partialView = PartialViews.Details;
        //    _idTicket = id;
            NavigationManager.NavigateTo($"/Companies/{Id}/Tickets/{id}");
            //StateHasChanged();
        }

        private void DetailsEditTicket()
        {
            _fromDetails = true;
            EditTicket(_idTicket);
        }

        private void TicketCloseForm()
        {
            _selectView = CompanyViews.Ticket;

            if (_fromDetails)
                _partialView = PartialViews.Details;
            else
                _partialView = PartialViews.Index;

            _fromDetails = false;
            StateHasChanged();
        }


        #endregion


        #region Contacts
        private void IndexEditContact(int? id)
        {
            _fromDetails = false;
            EditContact(id);
        }

        private void IndexNewContact()
        {
            _fromDetails = false;
            EditContact(null);
        }

        private void DetailsContact(int id)
        {
            _selectView = CompanyViews.Contacts;
            _partialView = PartialViews.Details;
            _idContact = id;

            StateHasChanged();
        }

        private void EditContact(int? id)
        {
            _selectView = CompanyViews.Contacts;
            _partialView = PartialViews.Edit;
            _idContact = id;

            StateHasChanged();
        }

        private void ContactCloseForm()
        {
            _selectView = CompanyViews.Contacts;

            if (_fromDetails)
                _partialView = PartialViews.Details;
            else
                _partialView = PartialViews.Index;

            _fromDetails = false;
            StateHasChanged();
        }

        #endregion

        #region Contracts

        private void IndexContracts()
        {
            _selectView = CompanyViews.Ticket;
            _partialView = PartialViews.Index;

            StateHasChanged();
        }
        private void EditContract(int? id)
        {
            _selectView = CompanyViews.Contracts;            
            _partialView = PartialViews.Edit;
            _idContract = id;

            StateHasChanged();
        }

        private void NewContract()
        {
            _fromDetails = false;
            EditContract(null);
        }
        private void IndexEditContract(int? id)
        {
            _fromDetails = false;
            EditContract(id);
        }

        private void DetailsContract(int id)
        {
            _selectView = CompanyViews.Contracts;
            _partialView = PartialViews.Details;
            _idContract = id;

            StateHasChanged();
        }

        private void DetailsEditContracts()
        {
            _fromDetails = true;
            EditTicket(_idTicket);
        }

        private void ContractCloseForm()
        {
            _selectView = CompanyViews.Contracts;

            if (_fromDetails)
                _partialView = PartialViews.Details;
            else
                _partialView = PartialViews.Index;

            _fromDetails = false;
            StateHasChanged();
        }


        #endregion

        #region Customers
        private async Task DetailsCustomer(int? id)
        {
            if (id != null)
            {
                Id = id.Value;
                
                _selectView = CompanyViews.Company;
                _partialView = PartialViews.Details;
                
                await LoadCompany();

                StateHasChanged();
            }
        }

        private async Task EditCustomer(int id)
        {
                Id = id;

                _selectView = CompanyViews.Company;
                _partialView = PartialViews.Edit;

                await LoadCompany();

                StateHasChanged();
        }
        private async Task RemoveCustomer(int idCustomer)
        {
            if (await DialogService.Confirm(Localize["Remove from customers?"]) == true)
            {
                await companiesService.RemoveCustomer(new CustomerModel() { IdReseller = Id, IdCustomer = idCustomer });
            }
        }

        private async Task AddCustomer(int idCustomer)
        {
            await companiesService.AddCustomer(new CustomerModel() { IdReseller = Id, IdCustomer = idCustomer });

            
        }
        #endregion
    }
}
