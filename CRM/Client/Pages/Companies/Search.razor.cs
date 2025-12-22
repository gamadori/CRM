using Microsoft.AspNetCore.Components;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Companies
{
    public partial class Search: ComponentBase
    {

        [Parameter]
        public EventCallback<int?> OnSelectComapany { get; set; }

        [Parameter]
        public int? IdCompanyParent { get; set; } = null;

        public bool _insertCompany = false;
        
        protected override async Task OnInitializedAsync()
        {

            await base.OnInitializedAsync();
        }

        private async Task LoadData()
        {

        }

        private void OnNewCompany()
        {
            _insertCompany = true;
            StateHasChanged();
        }

        private void OnEditClose()
        {

        }
    }
}
