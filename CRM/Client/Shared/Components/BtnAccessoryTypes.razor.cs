using CRM.Client.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components
{
    public partial class BtnAccessoryTypes : ComponentBase
    {
        [Inject]
        DialogService DialogService { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int? IdAccessoryType { get; set; } = null;

        [Parameter]
        public EventCallback<int?> OnGetItem { get; set; }

        private async Task OpenAccessoryTypes()
        {
            var id = await DialogService.OpenSideAsync<Pages.AccessoryTypes.Index>(Localize["Select Accessory Type"], new Dictionary<string, object>() { { "PageMode", PageHelper.PageModality.Dialog },
                    { "OnNewItem", OpenNewAccessoryType } },
                new SideDialogOptions { Position = DialogPosition.Top, ShowMask = false, Height = "auto", Style = "max-height: 90%;" });

            if (OnGetItem.HasDelegate)
                await OnGetItem.InvokeAsync(id);

        }


        private EventCallback OpenNewAccessoryType => new(null, (System.Action)(async () =>
        {
            DialogService.CloseSide();

            var id = await DialogService.OpenSideAsync<Pages.AccessoryTypes.Edit>(Localize["New Accessory Type"], new Dictionary<string, object>() { { "PageMode", PageHelper.PageModality.Dialog },
                { "OnClickCancel", AccessoryTypeOnClickCancel } },
                new SideDialogOptions { Position = DialogPosition.Top, ShowMask = false, Height = "auto", Style = "max-height: 90%;" });

            if (OnGetItem.HasDelegate)
                await OnGetItem.InvokeAsync(id);
                
        }));


        private void AccessoryTypeOnClickCancel()
        {
            DialogService.CloseSide();
        }

    }
}
