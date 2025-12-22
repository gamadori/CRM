using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Languages
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
        [Inject]
        private HttpClient _http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Parameter]
        public int? Id { get; set; }

        private Language _language = null;

        private RadzenFileInput<string> inputImage;

        private string _messageState = null;

        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {
                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.LanguagesPath;

                if (Id != null)
                {
                    path += $"/{Id}";

                   
                    _language = await _http.GetFromJsonAsync<Language>(path);
                }
                else
                    _language = new Language();
                
               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
                var resp = await RestClientService.Post<Language, int>(_language, ConstHelper.LanguagesPath);

                if (resp.State)
                {
                    NavigationManager.NavigateTo("/Settings/Languages/Index");
                }              
                else
                    _messageState = "Errore durante il salvataggio";
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        protected void Annulla()
        {
            NavigationManager.NavigateTo("/Settings/Languages/Index");
        }

        void Change(string value, string name)
        {
            
        }

        void Error(UploadErrorEventArgs args, string name)
        {
            
        }

    }
}
