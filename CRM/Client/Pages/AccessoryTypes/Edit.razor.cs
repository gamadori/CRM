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

namespace CRM.Client.Pages.AccessoryTypes
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        IAccessoryTypesService _service { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IEnumService EnumService { get; set; }


        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private AccessoryType _accessoryType = null;


        private string _messageState = "";

        private string _header = "Accessory Type";

       
        private int _pageSize = 12;

      
        protected override async Task OnInitializedAsync()
        {
            try
            {
              

                if (Id != null)
                {

                    _header = Localize["Edit Accessory Type"];
                    _accessoryType = await _service.Get(Id.Value);
                }
                else
                {
                    _header = "New Accessory Type";
                    _accessoryType = new AccessoryType();

                   
                }

               

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

       

      


       


        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
                var resp = await _service.Post(_accessoryType);
                if (resp != null)
                {
                    _accessoryType = resp.Data;
                    if (PageMode == PageModality.Dialog)
                    {
                        DialogService.CloseSide(_accessoryType.Id);
                    }
                    else if (OnClickSave != null)
                        OnClickSave();
                    else
                        NavigationManager.NavigateTo($"/{ConstHelper.ClientAccessoryTypesPath}");
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
                NavigationManager.NavigateTo($"/{ConstHelper.ClientAccessoryTypesPath}/Index");
        }

      


    }
}
