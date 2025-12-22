using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.LogEvents
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        public HttpClient Http { get; set; }
        
        [Parameter]
        public int Id { get; set; }

       
        [Parameter]
        public Action OnClickCancel { get; set; }

        private LogEvent _logEvent = null;

        protected override async Task OnInitializedAsync()
        {
  
            try
            {


                _logEvent = await Http.GetFromJsonAsync <LogEvent>($"{ConstHelper.LogEventsPath}/{Id}");

               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

      
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
             NavigationManager.NavigateTo("/LogEvents/Index");
        }


    }
}
