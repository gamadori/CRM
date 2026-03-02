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

namespace CRM.Client.Pages.Contacts
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        IContactsService Service { get; set; }

        [Inject]
        ICompaniesService CompaniesService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.Buttons> LocalizeBtn { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }
        
        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public int? Id { get; set; }


        [Parameter]
        public int? IdCompany { get; set; }
        
        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private Contact _contact = null;

        private List<CompanyDTO> _companies = new List<CompanyDTO>();

        private string _messageState = "";


        private int _companiesCount;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                await LoadCompanies();


                if (Id != null)
                {

                    var dto = await Service.GetItemAsync(Id.Value);
                    _contact = dto?.ToEntity();
                    

                }
                else
                {
                    _contact = new Contact();
                    
                    if (IdCompany != null)
                    {
                        _contact.IdCompany = (int)IdCompany;
                    }
                }
                //_pageHeader = HeaderService.Create("Contacts", Id, _contact?.NameComplete, true, ConstHelper.ClientContactsPath, null, PageMode);
                _pageHeader = await HeaderService.Create(PageMode);

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

       

        public async Task LoadCompanies()
        {

            _companies = await CompaniesService.GetListAsync(new CompanyFilter());
            
            await InvokeAsync(StateHasChanged);

        }

      

        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
                var resp = await Service.PostAsync(_contact);

                if (resp != null && resp.State)
                {
                    // ✅ Chiudi il dialog ritornando l'Id del contatto (nuovo o aggiornato)
                    int contactId = resp.Data?.Id ?? _contact.Id;
                    
                    if (OnClickSave != null)
                    {
                        OnClickSave();
                        // ✅ Chiudi il dialog Radzen ritornando l'Id
                        DialogService.CloseSide(contactId);
                    }
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
                await LoadCompanies();

                StateHasChanged();
                _contact.IdCompany = (int)id;
                StateHasChanged();
            }
        }
    }
}
