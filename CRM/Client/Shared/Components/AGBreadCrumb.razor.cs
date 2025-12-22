using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components
{
    public partial class AGBreadCrumb: ComponentBase
    {
        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localizer { get; set; }

        [Inject]
        IBreadCrumbService BreadCrumbService { get; set; }

        public enum BreadCrumbTypes
        {
            Settings,
            SMTP,
            TicketTypes
        }

        [Parameter]
        public List<BreadcrumbModel> Model { get; set; }

        [Parameter]
        public BreadCrumbTypes Type { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await Create();
            await base.OnInitializedAsync();
        }

        protected override void OnParametersSet()
        {
            StateHasChanged();
        }

        private async Task Goto(string url, Func<object, Task> action, object p)
        {
            if (action == null)
                NavigationManager.NavigateTo(url);
            else
                await action.Invoke(p);
        }

        private async Task Create()
        {
            Model = new List<BreadcrumbModel>();

            switch (Type)
            {
                case BreadCrumbTypes.Settings:
                    Model.AddRange(await BreadCrumbService.Settings());
                    break;

                case BreadCrumbTypes.SMTP:
                    Model = await  BreadCrumbService.SMTP();
                    break;
            }

        }
    }
}
