using Microsoft.AspNetCore.Components;
using System.Net.Http;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Prints
{
    public partial class PdfViewer: ComponentBase
    {
        [Inject]
        HttpClient Http { get; set; }

        [Parameter]
        public string Uri { get; set; }

        [Parameter]
        public string? Width { get; set; } // 200px o 20%
        [Parameter]
        public string? Height { get; set; } // 200px o 20%

        private string? _data = null;

        protected override async Task OnInitializedAsync()
        {


            await base.OnInitializedAsync();

        }

        private async Task LoadData()
        {
            _data = await Http.GetStringAsync(Uri);
        }
    }
}
