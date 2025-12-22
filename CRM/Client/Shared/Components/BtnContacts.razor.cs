using CRM.Client.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components
{
    public partial class BtnContacts: ComponentBase
    {
        [Inject]
        DialogService DialogService { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public bool Disabled { get; set; } = false;

        [Parameter]
        public EventCallback<int?> OnGetContacts { get; set; }

        [Parameter]
        public int? IdCompany { get; set; } 

        private async Task OpenContacts()
        {
            var id = await DialogService.OpenSideAsync<Pages.Contacts.Index>(Localize["Seleziona il contatto..."], new Dictionary<string, object>() { { "IdCompany", IdCompany }, { "PageMode", PageHelper.PageModality.Dialog },
                    { "OnClickNew", OpenNewContact } },
                new SideDialogOptions {  Position = DialogPosition.Top, ShowMask = false, Height = "auto",  Style= "max-height: 90%;" });

            if (OnGetContacts.HasDelegate)
                await OnGetContacts.InvokeAsync(id);

        }


        private EventCallback OpenNewContact => new(null, (System.Action)(async () =>
        {
            DialogService.CloseSide();

            var id = await DialogService.OpenSideAsync<Pages.Companies.Edit>(Localize["Nuovo Conttatto"], new Dictionary<string, object>() { { "PageMode", PageHelper.PageModality.Dialog },
                { "OnClickCancel", ContactOnClickCancel } },
                new SideDialogOptions { Position = DialogPosition.Top, ShowMask = false, Height = "auto", Style = "max-height: 90%;" });

            if (OnGetContacts.HasDelegate)
                await OnGetContacts.InvokeAsync(id);
                
        }));


        private void ContactOnClickCancel()
        {
            DialogService.CloseSide();
        }

    }
}
