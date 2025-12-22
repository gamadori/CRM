using CRM.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using CRM.Shared;
using MediatR;
using System;
using static CRM.Client.Program;
using System.Threading.Tasks;
using CRM.Client.Helpers;
using System.Linq;

namespace CRM.Client.Shared
{
    public partial class MainLayoutCustomer : LayoutComponentBase, INotificationHandler<MsgNotify>, IDisposable
    {
    
        [Inject]
        IAGRestClientService RestClientService { get; set; }


        [Inject]
        NotificationService NotificationService { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        private string _logo = null;
        protected override async Task OnInitializedAsync()
        {
            DynamicNotificationHandlers.Register(this);


            await LoadData();
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
            }
        }

        public async Task Handle(MsgNotify notification, System.Threading.CancellationToken cancellationToken)
        {
            var id = notification.Id;
            var sender = notification.Sender;
            Notify(Localize["New Message"], string.Format(Localize["New Message From"], sender), NotificationSeverity.Info);
        }

        private async void Notify(string summary, string msg, NotificationSeverity severity)
        {
            NotificationMessage message = new NotificationMessage() { Detail = msg, Severity = severity, Duration = 6000, Summary = summary, Style = "position: fixed; bottom: 0px !important;right: 0px !important;top: unset !important;" };
            NotificationService?.Notify(message);
            await _jsRuntime.InvokeAsync<string>("PlayAudio", "roar");

        }

        public void Dispose()
        {
            DynamicNotificationHandlers.Unregister(this);

        }
    }
}