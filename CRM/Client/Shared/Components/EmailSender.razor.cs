using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components
{
    public enum DocTypes
    {
        TicketIntervention,
        Ticket
    }
    public partial class EmailSender: ComponentBase
    {
        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public EventCallback OnClose { get; set; }

        [Parameter]
        public string SendEmailApiUrl { get; set; }

        [Parameter]
        public string EmailAddressesApiUrl{ get; set; }

        [Parameter]
        public DocTypes DocType { get; set; }



        private IEnumerable<string> _addresses;

        private EmailViewModel _email = new EmailViewModel();

        private bool _sending = false;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
        }

        private async Task Cancel()
        {
            if (OnClose.HasDelegate)
                await OnClose.InvokeAsync();
            else
                DialogService.Close();
        }

        protected async Task HandleValidSubmit()
        {
            bool state = false;

            if (await DialogService.Confirm(Localize["Inviare l'email con allegato il documento?"], Localize["Email"]) == true)
            {
                _sending = true;
                StateHasChanged();

                var resp = await HttpClient.PostAsJsonAsync<EmailViewModel>(SendEmailApiUrl, _email);

                if (resp.IsSuccessStatusCode)
                {
                    state = await resp.ReadAsync<bool>();

                   

                }

                _sending = false;

                StateHasChanged();

                if (state)
                {
                    Notify(Localize["Email Inviata"], NotificationSeverity.Success);
                    DialogService.Close();
                }
                else
                    Notify(Localize["Errore durante l'invio dell'email. Verifica i Log"], NotificationSeverity.Error);


                
                
            }
        }

        private void Notify(string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity };
            NotificationService?.Notify(message);
        }

        private async Task OnLoadData(LoadDataArgs args)
        {
            _addresses = await HttpClient.GetFromJsonAsync<List<string>>(EmailAddressesApiUrl);
            await InvokeAsync(StateHasChanged);
        }
    }
}
