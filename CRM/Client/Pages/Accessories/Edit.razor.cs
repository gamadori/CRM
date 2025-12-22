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

namespace CRM.Client.Pages.Accessories
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IAccessoriesService AccessoriesService { get; set; }
        [Inject]
        IAccessoryTypesService AccessoryTypesService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IEnumService EnumService { get; set; }


        [Parameter]
        public int? Id { get; set; }

        [Parameter] 
        public int? IdAccessoryType { get; set; }
        
        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        

        private Accessory _accessory = null;

        private List<AccessoryTypeModel> _accessoryTypes = null;


        private string _messageState = "";

        private string _header = "Accessory";

        private RadzenDropDown<int> _ddAccessoryTypes;


      
        protected override async Task OnInitializedAsync()
        {
            try
            {
              

                if (Id != null)
                {

                    _header = Localize["Edit Accessory"];
                    _accessory = await AccessoriesService.Get(Id.Value);
                }
                else
                {
                    _header = "New Accessory";


                    _accessory = new Accessory();

                    if (IdAccessoryType != null)
                        _accessory.IdAccessoryType = (int)IdAccessoryType;
                   
                }
                await LoadAccessoryTypes();



                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task LoadAccessoryTypes()
        {
            var resp = await AccessoryTypesService.GetItems(new AccessoryTypeFilter());

            if (resp != null)
            {
                _accessoryTypes = resp.Items;
            }
        }

      


       


        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
                var resp = await AccessoriesService.Post(_accessory);

                if (resp != null)
                {
                    _accessory = resp.Data;
                    if (PageMode == PageModality.Dialog)
                    {
                        DialogService.CloseSide(_accessory.Id);
                    }
                    else if (OnClickSave != null)
                        OnClickSave();
                    else
                        NavigationManager.NavigateTo($"/{ConstHelper.ClientAccessoriesPath}");
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
                NavigationManager.NavigateTo($"/{ConstHelper.ClientAccessoriesPath}/Index");
        }

        private async void OnGetAccessoryType(int? id)
        {
            if (id != null)
            {
                await LoadAccessoryTypes();
                await _ddAccessoryTypes.SelectItem(id, true);
                
            }
        }

        


    }
}
