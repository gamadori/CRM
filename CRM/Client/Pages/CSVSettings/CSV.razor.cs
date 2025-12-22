using CRM.Client.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.CSVSettings
{
    public partial class CSV : ComponentBase
    {
        [Inject]
        public HttpClient HttpClient { get; set; }

        [Inject]
        public NavigationManager NavigationManager {get; set;}

        [Parameter]
        public string TableName { get; set; }


        private List<CSVMapping> _csvMapping = null;
        
        private string _msgError = "";
        protected override async void OnInitialized()
        {
            try
            {
                _msgError = "";
                _csvMapping = await HttpClient.GetFromJsonAsync<List<CSVMapping>>($"{ConstHelper.CSVPath}/{TableName}");

                if (_csvMapping == null)
                    _csvMapping = new List<CSVMapping>();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                _msgError = ex.Message;

            }
            finally
            {
                base.OnInitialized();
            }
        }

        protected async Task OnSubmit(List<CSVMapping> mapping)
        {
            var resp = await HttpClient.PostAsJsonAsync<List<CSVMapping>>($"{ConstHelper.CSVPath}/Mapping/{TableName}", mapping.ToList());
            NavigationManager.NavigateTo($"CSVSettings/Details/{TableName}");
        }

        
    }
}
