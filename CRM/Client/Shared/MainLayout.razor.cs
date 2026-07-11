using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using static CRM.Client.Program;

namespace CRM.Client.Shared
{
    public partial class MainLayout: LayoutComponentBase, INotificationHandler<MsgNotify>, INotificationHandler<InboundEmailNotify>, IDisposable
    {
        [Inject]
        IAGRestClientService RestClientService { get; set; }


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

        private string _logo = null;

        private System.Threading.Timer? timer = null;

        private string? _iconConnected = null;

        private bool? _isConnected = null;

        private string _bgColor = "#f7f7f7";

        protected override async Task OnInitializedAsync()
        {
            DynamicNotificationHandlers.Register(this);

            var authState = await AuthProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (!user.Identity?.IsAuthenticated ?? true)
            {
                return;
            }

            await LoadData();

            timer = new System.Threading.Timer(async (object? stateInfo) =>
            {
                bool isConnected;
                HubHelper.Timertick();

                isConnected = HubHelper.IsConnected();

                if (isConnected)
                    _iconConnected = "wifi";
                else
                    _iconConnected = "wifi_off";

                if (_isConnected != isConnected)
                {
                    _isConnected = isConnected;
                    StateHasChanged();
                }
            }, new System.Threading.AutoResetEvent(false), 5000, 5000); // fire every 2000 milliseconds

            await base.OnInitializedAsync();
        }

        private async Task LoadData()
        {
            var settings = await RestClientService.GetFirst<GlobalSetting>(ConstHelper.GlobalSettingsPath);

            if (settings != null && settings.LogoSiteHeader != null)
            {
                var logo = await RestClientService.GetItem<Logo, int>((int)settings.LogoSiteHeader, ConstHelper.LogosPath);
                
                if (logo != null && logo.InputFile.Any())
                {
                    _logo = logo.InputFile;

                    
                }
                _bgColor = settings.TopRowBgColor;
            }
        }
        public async Task Handle(MsgNotify notification, System.Threading.CancellationToken cancellationToken)
        {
            var id = notification.Id;
            var sender = notification.Sender;
            Notify(Localize["New Message"], string.Format(Localize["New Message From"], sender), NotificationSeverity.Info);
        }

        public async Task Handle(InboundEmailNotify notification, System.Threading.CancellationToken cancellationToken)
        {
            var from = string.IsNullOrWhiteSpace(notification.From) ? "sconosciuto" : notification.From;
            Notify("Nuova email", $"Da {from}: {notification.Subject}", NotificationSeverity.Info);
        }

       

        private async void Notify(string summary, string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity, Duration=6000, Summary = summary, Style= "position: fixed; bottom: 0px !important;right: 0px !important;top: unset !important;" };
            NotificationService?.Notify(message);
            await JsRuntime.InvokeAsync<string>("PlayAudio", "roar");

        }

        public void Dispose()
        {
            DynamicNotificationHandlers.Unregister(this);
            timer?.Dispose();
        }

        public async Task HandleException(Exception exception)
        {
            if (exception.Message.Contains("401"))
            {
                await SignOutManager.SetSignOutState();
               // navigationManager.NavigateTo("/login/Expired", true);
                navigationManager.NavigateToLogout("/login/Expired");
                return;
            }
            //If you use AutoWrapper or other packages, you can send messages here based on your response type, don't forget to parse the values. 
        }
    }
}
