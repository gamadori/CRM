using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ContractTypes
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IContractTypesService Service { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IBreadCrumbService BreadCrumbService { get; set; }
        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action<int>OnClickEditChild { get; set; } 

        [Parameter]
        public Action OnClickCancel { get; set; }

        private ContractType _item = null;

        private List<BreadcrumbModel> _bread = null;

        private string _title;

        protected override async Task OnInitializedAsync()
        {
  
            try
            {

                _item = await Service.Get(Id);

                _title = Localize["Details"];

                if (OnClickEdit == null)
                    _bread = await BreadCrumbService.ContractTypes(false, _item.Name);

               


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

      
        protected void Edit()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientContractTypesPath}/Edit/{Id}");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientContractTypesPath}/Index");
        }


    }
}
