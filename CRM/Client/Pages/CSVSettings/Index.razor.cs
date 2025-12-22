using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System.Collections.Generic;

namespace CRM.Client.Pages.CSVSettings
{
    public partial class Index : ComponentBase
    {


        [Inject]
        private NavigationManager NavigationManager { get; set; }

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        protected override void OnInitialized()
        {
            _bread.Add(new BreadcrumbModel() { Title = $"Settings", Url = $"Settings" });
            _bread.Add(new BreadcrumbModel() { Title = $"CSV", Url = null });
            base.OnInitialized();
        }

    }
}
