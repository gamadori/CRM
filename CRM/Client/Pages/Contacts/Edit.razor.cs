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

namespace CRM.Client.Pages.Contacts
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        IAGRestClientService RestClientService { get; set; }
        
        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.Buttons> LocalizeBtn { get; set; }

        [Parameter]
        public int? Id { get; set; }


        [Parameter]
        public int? IdCompany { get; set; }
        
        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        

        private Contact _contact = null;

        private List<Company> _companies = new List<Company>();

        private string _messageState = "";

        private string _header = "Contact";

        private int _companiesCount;

        private RadzenDropDownDataGrid<int?> _ddCompany;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                await LoadCompany();


                if (Id != null)
                {

                    _header = Localize["Edit Contact"];
                    _contact = await RestClientService.GetItem<Contact, int>(Id.Value, ConstHelper.ContactsPath);
                }
                else
                {
                    _header = Localize["New Contact"];
                    _contact = new Contact();

                    if (IdCompany != null)
                    {
                        _contact.IdCompany = (int)IdCompany;
                    }
                }

               

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

       

        public async Task LoadCompany()
        {

            var items = await RestClientService.Get<Company>(ConstHelper.CompaniesPath);
            _companiesCount = items.Count;

            _companies = items;
            await InvokeAsync(StateHasChanged);

        }

      

        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
                var resp = await RestClientService.Post<Contact, int>(_contact, ConstHelper.ContactsPath);

                if (resp != null && resp.State)
                {
                    if (OnClickSave != null)
                        OnClickSave();
                    else
                        NavigationManager.NavigateTo($"/{ConstHelper.ClientContactsPath}");
                }
                else
                    _messageState = "Errore durante il salvataggio";
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientContactsPath}/Index");
        }
        private async Task OnGetCompany(int? id)
        {
            if (id != null)
            {
                await LoadCompany();
                await _ddCompany.SelectItem(id, true);

            }
        }

    }
}
