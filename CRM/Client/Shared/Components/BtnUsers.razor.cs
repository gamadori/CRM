using CRM.Client.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components
{
    public partial class BtnUsers: ComponentBase
    {
        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public string Text { get; set; } = string.Empty;

        [Parameter]
        public bool Disabled { get; set; } = false;

        [Parameter]
        public int? IdProjectParent { get; set; } = null;

        [Parameter]
        public int? IdCompany { get; set; } = null;
        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public EventCallback<string?> OnGetItem { get; set; }

        private async Task OpenItems()
        {
            var id = await DialogService.OpenSideAsync<Pages.Settings.Users.Index>(Localize["Seleziona l'Utente..."], new Dictionary<string, object>() { { "PageMode", PageHelper.PageModality.Dialog },
                    { "OnClickEdit", OpenNewItem }, {"IdProjectParent", IdProjectParent }, {"IdCompany", IdCompany } },
                new SideDialogOptions { Position = DialogPosition.Top, ShowMask = false, Height = "auto", Style = "max-height: 90%;" });

            if (OnGetItem.HasDelegate)
                await OnGetItem.InvokeAsync(id);

        }


        private EventCallback<string?> OpenNewItem => new(null, (System.Action)(async () =>
        {
            DialogService.CloseSide();

            var id = await DialogService.OpenSideAsync<Pages.Settings.Users.Edit>(Localize["Nuova Utente"], new Dictionary<string, object>() { { "PageMode", PageHelper.PageModality.Dialog },
                { "CloseForm", ItemOnClickCancel } },
                new SideDialogOptions { Position = DialogPosition.Top, ShowMask = false, Height = "auto", Style = "max-height: 90%;" });

            if (OnGetItem.HasDelegate)
                await OnGetItem.InvokeAsync(id);
                
        }));


        private void ItemOnClickCancel()
        {
            DialogService.CloseSide();
        }

    }
}
