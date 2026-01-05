using CRM.Client.Helpers;
using CRM.Client.Models;
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

namespace CRM.Client.Pages.ProductAccTypes
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        IProductAccTypesService  ProductTypeAccsService { get; set; }

        [Inject]
        IAccessoryTypesService AccessoryTypesService { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public int? IdProduct { get; set; } 

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private ProductAccessoryType _productTypeAccessory = null;

        private List<AccessoryTypeModel> _accessoryTypes = new List<AccessoryTypeModel>();
        
        private string _messageState = "";

        private RadzenDropDown<int> _ddAccessoryTypes;

        private int _pageSize = 12;
        
        private PageHeaderModel _pageHeader = new PageHeaderModel();

        protected override async Task OnInitializedAsync()
        {
            try
            {

                await LoadAccessoryTypes();

                if (Id != null)
                {
                    _productTypeAccessory = await ProductTypeAccsService.Get(Id.Value);
                }
                else
                {
                    _productTypeAccessory = new ProductAccessoryType() { IdProduct = (int)IdProduct };
                }

                _pageHeader = await HeaderService.Create(PageMode);

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
                StateHasChanged();
            }

            
        }

        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
                var resp = await ProductTypeAccsService.Post(_productTypeAccessory);
                if (resp != null)
                {
                    _productTypeAccessory = resp.Data;
                    if (PageMode == PageModality.Dialog)
                    {
                        DialogService.CloseSide(_productTypeAccessory.Id);
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
