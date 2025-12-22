using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json.Bson;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.CompanyContracts
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        ICompanyContractsService  Service { get; set; }

        [Inject]
        IContractTypesService ContractTypeService { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

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

        private CompanyContract _item = null;

        private List<ContractType> _contractTypes = new List<ContractType>();
        
        private string _messageState = "";

        private string _header = "Contracts";

        private RadzenDropDown<int> _ddContractType;

        private int _pageSize = 12;

      
        protected override async Task OnInitializedAsync()
        {
            try
            {

                await LoadContractTypes();

                if (Id != null)
                {

                    _header = Localize["Edit"];
                    _item = await Service.Get(Id.Value);
                }
                else
                {
                    _header = "New";
                    _item = new CompanyContract() { IdCompany = (int)IdCompany, DateFrom = DateTime.Today, DateTo = DateTime.Today.AddYears(1) };

                   
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task LoadContractTypes()
        {
            var resp = await ContractTypeService.GetList(new ContractTypeFilter());

            if (resp != null)
            {
                _contractTypes = resp.Items;
                StateHasChanged();
            }

            
        }


        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
                if (_item.Id == 0 && await CheckContracts())
                {
                    return;
                }
    

                var resp = await Service.Post(_item);
                if (resp != null)
                {
                    _item = resp.Data;
                    if (PageMode == PageModality.Dialog)
                    {
                        DialogService.CloseSide(_item.Id);
                    }
                    else if (OnClickSave != null)
                        OnClickSave();
                    else
                        NavigationManager.NavigateTo($"/{ConstHelper.ClientCompanyContractsPath}");
                }
                else
                    _messageState = Localize["Errore durante il salvataggio"];
               
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        /// <summary>
        /// Verifica se ci sono dei contratti attivi per il cliente.
        /// Nel caso ci fossero chiede se devono essere sostituiti
        /// </summary>
        /// <returns>
        /// false: se non ci sono dei contratti attivi oppure se devono essere sostituiti
        /// true: altrimenti
        /// </returns>
        private async Task<bool> CheckContracts()
        {
            var contracts = await Service.CheckContractActive(_item);
            if (contracts != null)
            {
                if (contracts.Any())
                {
                    if (await DialogService.Confirm(Localize["Ci sono dei contratti attivi, occorre disabilitarli prima di sostituirli, procedere?"]) == true)
                    {
                        _item.Confirm = true;
                        return false;
                    }
                    else
                    {
                        _messageState = Localize["Ci sono dei contratti attivi, occorre disabilitarli prima di sostituirli"];
                        return true;
                    }
                }
            }
            else
                _messageState = Localize["Errore durante il salvataggio"];
            return false;
                
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientCompanyContractsPath}/Index");
        }


        private async void OnGetContractTypes(int? id)
        {
            if (id != null)
            {
                await LoadContractTypes();
                await _ddContractType.SelectItem(id, true);

            }
        }

        private void OnChangeContract(object id)
        {
            var contract = _contractTypes.Where(x => x.Id == (int)id).FirstOrDefault();

            if (contract != null)
                _item.Price = contract.Price;
            else
                _item.Price = 0;
        }

    }
}
