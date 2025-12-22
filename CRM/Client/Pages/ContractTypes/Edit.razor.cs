using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen.Blazor;
using Syncfusion.Blazor.Popups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ContractTypes
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
        [Inject]
        private IContractTypesService Service { get; set; }


        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IBreadCrumbService BreadCrumbService { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        private ContractType _item = null;

        private string _title = null;

        private bool _dialogVisible = false;

        private SfDialog _dialogPopUp;

        private List<BreadcrumbModel> _bread = null;

        

        protected override async Task OnInitializedAsync()
        {
           
            try
            {


                
                if (Id != null)
                {
                    _title = Localize["Edit"];
                    _item = await Service.Get(Id.Value);
                }
                else
                {
                    _title = Localize["New"];
                    _item = new ContractType();
                }
                if (OnClickSave == null)
                    _bread = await BreadCrumbService.ContractTypes(true, _title);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected async Task HandleValidSubmit()
        {

            try
            {
                _item = (await Service.Post(_item)).Data;
                
                if (OnClickSave != null)
                    OnClickSave();
                else
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientContractTypesPath}/Index");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        protected void Cancel()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientContractTypesPath}/Index");

           
        }

     

       

    }
}
