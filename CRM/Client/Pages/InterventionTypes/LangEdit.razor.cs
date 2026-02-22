using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.Resources.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace CRM.Client.Pages.InterventionTypes
{
    public partial class LangEdit: ComponentBase
    {
        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }
        
        [Parameter]
        public int Id { get; set; }

        private InterventionType _interventionType;
        
        private string _messageState;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
            await base.OnInitializedAsync();
        }

        private async Task LoadData()
        {
            _interventionType = await HttpClient.GetFromJsonAsync<InterventionType>($"{ConstHelper.InterventionTypeLangsPath}?Id={Id}");
        }

        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
                var resp = await HttpClient.PostAsJsonAsync<InterventionType>(ConstHelper.InterventionTypeLangsPath, _interventionType);


                if (resp.IsSuccessStatusCode)
                {

                    NavigationManager.NavigateTo($"Settings/InterventionTypes/{_interventionType.Id}");
                }
                else
                {
                    
                    Notify(Localize["Errore durante il salvataggio"], NotificationSeverity.Error);
                }
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        private void Cancel()
        {
            NavigationManager.NavigateTo($"Settings/InterventionType/{_interventionType.Id}");
        }

        private void Notify(string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity };
            NotificationService?.Notify(message);
        }
    }
}
