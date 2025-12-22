using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.CSVSettings
{
    public partial class CSVData: ComponentBase
    {
        [Inject]
        public HttpClient HttpClient { get; set; }

        [Inject]
        public NavigationManager NavigationManager { get; set; }

        [Parameter]
        public string TableName { get; set; }

        [Parameter]
        public string Parent { get; set; }

        private List<CSVMapping> _csvMapping = null;

        private string _msgError = null;
      
        private string _urlApi;

        private string _urlPage;
        protected override async Task OnInitializedAsync()
        {
            try
            {
                switch (TableName)
                {
                    case "Company":
                        _urlApi = $"{ConstHelper.CSVPath}/Company";
                        _urlPage = "Companies";

                        break;
                    case "Category":
                        _urlApi = $"{ConstHelper.CSVPath}/Category";
                        _urlPage = "Products";
                        break;
                    case "Article":
                        _urlApi = $"{ConstHelper.CSVPath}/Article";
                        _urlPage = "Articles";
                        break;
                }

                _msgError = null;
                _csvMapping = await HttpClient.GetFromJsonAsync<List<CSVMapping>>(_urlApi);

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

        protected void OnUpload(bool state)
        {
            if (state)
            {
                NavigationManager.NavigateTo(_urlPage);
            }
            else
            {
                _msgError = "Errore durante l'importazione dei dati";
                StateHasChanged();
            }
        }



        
    }
}
