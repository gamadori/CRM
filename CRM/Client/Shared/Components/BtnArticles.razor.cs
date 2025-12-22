using CRM.Client.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components
{
    public partial class BtnArticles: ComponentBase
    {
        [Inject]
        DialogService DialogService { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int? IdCompany { get; set; } = null;

        [Parameter]
        public EventCallback<int?> OnGetArticle { get; set; }

        [Parameter]
        public bool Disabled { get; set; } = false;

        private async Task OpenArticles()
        {
            var id = await DialogService.OpenSideAsync<Pages.Articles.Index>(Localize["Seleziona Articolo"], new Dictionary<string, object>() { { "PageMode", PageHelper.PageModality.Dialog },
                    { "OnNewArticle", OpenNewArticle }, {"IdCompany", IdCompany } },
                new SideDialogOptions { Position = DialogPosition.Top, ShowMask = false, Height = "auto", Style = "max-height: 90%;" });

            if (OnGetArticle.HasDelegate)
                await OnGetArticle.InvokeAsync(id);

        }


        private EventCallback OpenNewArticle => new(null, (System.Action)(async () =>
        {
            DialogService.CloseSide();

            var id = await DialogService.OpenSideAsync<Pages.Articles.Edit>(Localize["Nuovo Articolo"], new Dictionary<string, object>() { { "PageMode", PageHelper.PageModality.Dialog },
                { "OnClickCancel", ArticleOnClickCancel }, {"IdCompany", IdCompany } },
                new SideDialogOptions { Position = DialogPosition.Top, ShowMask = false, Height = "auto", Style = "max-height: 90%;" });

            if (OnGetArticle.HasDelegate)
                await OnGetArticle.InvokeAsync(id);
                
        }));


        private void ArticleOnClickCancel()
        {
            DialogService.CloseSide();
        }

    }
}
