using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Radzen;

namespace CRM.Client.Pages.EmailsSent
{
    [Authorize]
    public partial class Details : ComponentBase
    {
        
        [Inject]
        private IBaseRestService<EmailSent, EmailSentFilterModel, int> _service { get; set; }

        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> _userService { get; set; }
        [Inject]
        private HttpClient _httpClient { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public int? Id { get; set; }


        [Parameter]
        public EventCallback OnClickCancel { get; set; }


        protected enum MsgBoxComand
        {
            ViewReport,
            CreateReport,
            DownloadReport
        }

        
        private EmailSent _email = null;

        private ApplicationUser _user = null;

        private List<EmailEvent> _events = new();


        protected override async Task OnInitializedAsync()
        {
            await LoadData();
        }

        protected async Task LoadData()
        {
            try
            {

                if (Id != null)
                {

                    _email = await _service.Get(Id.Value);
                    _events = await _httpClient.GetFromJsonAsync<List<EmailEvent>>($"api/EmailsSent/{Id.Value}/events") ?? new();
                }
                else
                    _email = new EmailSent();

                _user = await _userService.Get(_email.IdUser);
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }


        protected async Task Annulla()
        {
            if (OnClickCancel.HasDelegate)
                await OnClickCancel.InvokeAsync();
            else
                NavigationManager.NavigateTo("/EmailSent/Index");
        }

        


      

    }
}
