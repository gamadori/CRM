using CRM.Client.Services;
using Microsoft.AspNetCore.Components;
using Syncfusion.Blazor;
using Syncfusion.Blazor.PdfViewer;
using Syncfusion.Blazor.SfPdfViewer;
using Syncfusion.PdfExport;
using System;
using System.Buffers.Text;
using System.IO;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace CRM.Client.Pages.Tickets
{
    
    public partial class Report: ComponentBase
    {
        [Inject]
        public ITicketService Service { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public string? Width { get; set; } = "100%"; // 200px o 20%

        [Parameter]
        public string? Height { get; set; } = "100%"; // 200px o 20%


        private string? _data = null;

        private SfPdfViewer2 pdfViewer;
        

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
            await base.OnInitializedAsync();
        }


        private async Task LoadData()
        {
            var pdf = await Service.Print(Id);
            _data = "data:application/pdf;base64," + pdf;
            
            StateHasChanged();
        }
            
    }
}
