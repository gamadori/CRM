using CRM.Client.Services;
using CRM.Shared;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System;
using System.Threading.Tasks;
using static CRM.Client.Program;

namespace CRM.Client.Shared
{
    public partial class PrintLayout: LayoutComponentBase, INotificationHandler<MsgNotify>, IDisposable
    {
        [Inject]
        IJSRuntime JsRuntime { get; set; }

        [Inject] 
        NavigationManager navigationManager { get; set; }
           
        [Inject]
        IAccessTokenProvider tokenProvider { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        AuthenticationStateProvider AuthProvider { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        SignOutSessionStateManager SignOutManager { get; set; }

        protected override async Task OnInitializedAsync()
        {
            DynamicNotificationHandlers.Register(this);

            var authState = await AuthProvider.GetAuthenticationStateAsync();

            var user = authState.User;
            

            await base.OnInitializedAsync();
            
        }

        public async Task Handle(MsgNotify notification, System.Threading.CancellationToken cancellationToken)
        {
            var id = notification.Id;
            var sender = notification.Sender;
            Notify(Localize["New Message"], string.Format(Localize["New Message From"], sender), NotificationSeverity.Info);
        }

       

        private async void Notify(string summary, string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity, Duration=6000, Summary = summary, Style= "position: fixed; bottom: 0px !important;right: 0px !important;top: unset !important;" };
            NotificationService?.Notify(message);
            await _jsRuntime.InvokeAsync<string>("PlayAudio", "roar");

        }

        public void Dispose()
        {
            DynamicNotificationHandlers.Unregister(this);

        }

        public async Task HandleException(Exception exception)
        {
            if (exception.Message.Contains("401"))
            {
                await SignOutManager.SetSignOutState();
                navigationManager.NavigateTo("/login/Expired", true);
                return;
            }
            //If you use AutoWrapper or other packages, you can send messages here based on your response type, don't forget to parse the values. 
        }
    }
}
