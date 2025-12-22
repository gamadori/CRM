using Microsoft.AspNetCore.Components;
using BlazoringComponents.ImportDataFile;

namespace CRM.Client.Pages.CSVSettings
{
    public partial class Details: ComponentBase
    {
        [Parameter]
        public string TableName { get; set; }


        private string _pageName { get; set; }

        protected override void OnInitialized()
        {
            switch (TableName)
            {
                case "Campany":
                    _pageName = "Companies";
                    break;

                case "Category":
                    _pageName = "Products";
                    break;

                case "Article":
                    _pageName = "Articles";
                    break;

            }
            base.OnInitialized();
        }
    }
}
