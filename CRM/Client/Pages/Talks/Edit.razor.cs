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

namespace CRM.Client.Pages.Talks
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
        DialogService DialogService { get; set; }

        [Inject]
        IEnumService EnumService { get; set; }


        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public int? IdParent { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }
        
        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private Talk _talk = null;

        private List<Company> _companies = new List<Company>();

        private List<Contact> _contacts = new List<Contact>();

        private List<EnumField> _talkPhases;

        private List<EnumField> _talkStates;

        private string _messageState = "";

        private string _header = "Talk";

        private bool _lockCompany = false;

        private int _pageSize = 12;

       

        private RadzenDropDownDataGrid<int?> _ddCompany;

        private RadzenDropDownDataGrid<int?> _ddContact;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                LoadStates();

                LoadPhases();

                await LoadCompany();

               // await LoadContacts();

                if (Id != null)
                {

                    _header = Localize["Edit Talk"];
                    _talk = await RestClientService.GetItem<Talk, int>(Id.Value, ConstHelper.TalkPath);
                }
                else
                {
                    _header = "New Talk";
                    _talk = new Talk();

                    if (IdCompany != null)
                    {
                        _talk.IdCompany = IdCompany;
                        _lockCompany = true;
                    }
                }

               

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

       

        private async Task LoadCompany()
        {

            var items = await RestClientService.Get<Company>(ConstHelper.CompaniesPath); 
           

            _companies = items;
            await InvokeAsync(StateHasChanged);

        }


        private async Task LoadContacts()
        {
           
            var response = await RestClientService.GetListPag<ContactFilter, Contact>(new ContactFilter() { IdCompany = _talk?.IdCompany }, ConstHelper.ContactsPath);

            _contacts = response?.Items;
           
         
        }

        private void LoadPhases()
        {
            _talkPhases = EnumService.EnumGetList(typeof(TalkPhases));
        }

        private void LoadStates()
        {
            _talkStates = EnumService.EnumGetList(typeof(TalkStates));
        }

        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
                var resp = await RestClientService.Post<Talk, int>(_talk, ConstHelper.TalkPath);
                if (resp != null)
                {
                    _talk = resp.Data;
                    if (PageMode == PageModality.Dialog)
                    {
                        DialogService.CloseSide(_talk.Id);
                    }
                    else if (OnClickSave != null)
                        OnClickSave();
                    else
                        NavigationManager.NavigateTo($"/{ConstHelper.ClientTalkPath}");
                }
                else
                    _messageState = Localize["Errore durante il salvataggio"];
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
                NavigationManager.NavigateTo($"/{ConstHelper.ClientTalkPath}/Index");
        }

        private async Task OnChangeCompany()
        {
            await LoadContacts();
        }
        private async Task OnGetCompany(int? id)
        {
            if (id != null)
            {
                await LoadCompany();
                await _ddCompany.SelectItem(id, true);

            }
        }

        private async Task OnGetContact(int? id)
        {
            if (id != null)
            {
                await LoadContacts();
                await _ddContact.SelectItem(id, true);

            }
        }


    }
}
