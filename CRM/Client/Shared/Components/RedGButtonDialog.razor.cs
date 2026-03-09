using CRM.Shared.Constants;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components
{
    public partial class RedGButtonDialog: ComponentBase
    {
        [Inject]
        DialogService DialogService { get; set; } = default!;

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; } = default!;

        [Parameter]
        public EventCallback<object?> OnSelectItem { get; set; }
        [Parameter]
        public EventCallback OnAddNewItem { get; set; }

        [Parameter]
        public RedGDialog.DialogType DialogType { get; set; } = RedGDialog.DialogType.None;

        [Parameter]
        public RedGDialog.DialogMode DialogMode { get; set; } = RedGDialog.DialogMode.Selection;

        [Parameter]
        public object? IdParent { get; set; }

        private string _icon = "search";

        private string _tile = string.Empty;
        protected override void OnInitialized()
        {
            GetIcon();
            base.OnInitialized();
        }
        private async Task OnClick()
        {
            
            await OpenDialog();
        }

        private async Task OpenDialog()
        {
            var id = await DialogService.OpenSideAsync<RedGDialog>(_tile, new Dictionary<string, object>()
            {
                { "Type", DialogType },
                { "Mode", DialogMode },
                { "OnAddNewItem", EventCallback.Factory.Create(this, OnAddNewItem) },
                 { "IdParent", IdParent } },
    new SideDialogOptions { Position = DialogPosition.Top, ShowMask = false, Height = "auto", Style = "max-height: 90%;" });

            if (OnSelectItem.HasDelegate)
                await OnSelectItem.InvokeAsync(id);
        }

        private void GetIcon()
        {
            switch (DialogType)
            {
                case RedGDialog.DialogType.Companies:
                    _icon = MaterialIcons.Company;
                    _tile = Localize["Companies"];
                    break;
                case RedGDialog.DialogType.Users:
                    _icon = MaterialIcons.Users;
                    _tile = Localize["Users"];
                    break;
                case RedGDialog.DialogType.Contacts:
                    _icon = MaterialIcons.Contacts;
                    _tile = Localize["Contacts"];
                    break;
                default:

                    _icon = "search";
                    break;
            }
        }
    }
}
