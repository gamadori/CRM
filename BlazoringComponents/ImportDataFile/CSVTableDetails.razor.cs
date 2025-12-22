using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using BlazoringComponents.ImportDataFile;

namespace BlazoringComponents.ImportDataFile
{
    public partial class CSVTableDetails: ComponentBase
    {
        [Inject]
        public HttpClient HttpClient { get; set; }

        [Parameter]
        public string TableName { get; set; }

        [Parameter]
        public string Url { get; set; }

        

        private List<CSVMapping> _csvMapping;
        protected override async Task OnInitializedAsync()
        {
            _csvMapping = await HttpClient.GetFromJsonAsync<List<CSVMapping>>($"{Url}/{TableName}");

            await base.OnInitializedAsync();
        }
    }
}
