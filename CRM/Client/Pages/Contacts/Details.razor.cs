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

namespace CRM.Client.Pages.Contacts
{
    [Authorize]
    public partial class Details: ComponentBase
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
        public int Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

       
        [Parameter]
        public Action OnClickCancel { get; set; }

        private Contact _contact = null;

        protected override async Task OnInitializedAsync()
        {
  
            try
            {


                _contact = await RestClientService.GetItem<Contact, int>(Id, ConstHelper.ContactsPath);

               
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
                NavigationManager.NavigateTo($"/Contacts/Edit/{Id}");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
             NavigationManager.NavigateTo("/Contacts/Index");
        }


    }
}
