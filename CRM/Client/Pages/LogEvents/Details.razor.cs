using CRM.Client.Helpers;
using CRM.Client.Models;
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
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.LogEvents
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        
        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        HttpClient Http { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private LogEvent _logEvent = null;

        private PageHeaderModel? _pageHeader = null;
        protected override async Task OnInitializedAsync()
        {
  
            try
            {
                _logEvent = await Http.GetFromJsonAsync <LogEvent>($"{ConstHelper.LogEventsPath}/{Id}");

                _pageHeader = await HeaderService.Create(PageMode);               
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
             NavigationManager.NavigateTo(ConstHelper.ClientLogEventsPath);
        }


    }
}
