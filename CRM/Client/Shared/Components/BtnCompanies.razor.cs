using CRM.Client.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components
{
    public partial class BtnCompanies: ComponentBase
    {
        [Inject]
        DialogService DialogService { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public string Text { get; set; } = string.Empty;

        [Parameter]
        public bool Disabled { get; set; } = false;

        [Parameter]
        public EventCallback<int?> OnGetCompany { get; set; }

        [Parameter]
        public int? IdCompanyParent { get; set; } = null;

        private async Task OpenCompanies()
        {
            var id = await DialogService.OpenSideAsync<Pages.Companies.Index>(Localize["Seleziona la Ditta..."], new Dictionary<string, object>() { { "PageMode", PageHelper.PageModality.Dialog}, {"IdCompanyParent", IdCompanyParent },
                    { "OnAddNewItem", OpenNewCompany } },
                new SideDialogOptions {  Position = DialogPosition.Top, ShowMask = false, Height = "auto",  Style= "max-height: 90%;" });

            if (OnGetCompany.HasDelegate)
                await OnGetCompany.InvokeAsync(id);

        }


        private EventCallback OpenNewCompany => new(null, (System.Action)(async () =>
        {
            DialogService.CloseSide();

            var id = await DialogService.OpenSideAsync<Pages.Companies.Edit>(Localize["Nuova Ditta"], new Dictionary<string, object>() { { "PageMode", PageHelper.PageModality.Dialog },
                { "OnClickCancel", CompanyOnClickCancel } },
                new SideDialogOptions { Position = DialogPosition.Top, ShowMask = false, Height = "auto", Style = "max-height: 90%;" });

            if (OnGetCompany.HasDelegate)
                await OnGetCompany.InvokeAsync(id);
                
        }));


        private void CompanyOnClickCancel()
        {
            DialogService.CloseSide();
        }

    }
}
