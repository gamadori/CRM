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

namespace CRM.Client.Pages.ContractTypeTicketTypes
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        IContractTypeTicketService  Service { get; set; }

        [Inject]
        ITicketTypesService TicketTypesService { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public int? IdContractType { get; set; } 

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private ContractTypeTicketType _item = null;

        private List<TicketType> _ticketTypes = new List<TicketType>();
        
        private string _messageState = "";

        private string _header = "Ticket Types";

        private RadzenDropDown<int> _ddTicketTypes;

        private int _pageSize = 12;

      
        protected override async Task OnInitializedAsync()
        {
            try
            {

                await LoadTicketTypes();
                if (Id != null)
                {

                    _header = Localize["Edit"];
                    _item = await Service.Get(Id.Value);
                }
                else
                {
                    _header = Localize["New"];
                    _item = new ContractTypeTicketType() { IdContractType = (int)IdContractType };

                   
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task LoadTicketTypes()
        {
            var resp = await TicketTypesService.GetList(new TicketTypeFilter());

            if (resp != null)
            {
                _ticketTypes = resp.Items;
                StateHasChanged();
            }

            
        }

        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
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
                        NavigationManager.NavigateTo($"/{ConstHelper.ClientProductAccTypesPath}");
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
                NavigationManager.NavigateTo($"/{ConstHelper.ClientProductAccTypesPath}/Index");
        }


        private async void OnGetTicketType(int? id)
        {
            if (id != null)
            {
                await LoadTicketTypes();
                await _ddTicketTypes.SelectItem(id, true);

            }
        }

    }
}
