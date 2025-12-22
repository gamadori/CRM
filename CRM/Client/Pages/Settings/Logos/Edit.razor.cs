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

namespace CRM.Client.Pages.Settings.Logos
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }
        

        [Parameter]
        public int? Id { get; set; }

        private Logo _logo = null;

        private RadzenFileInput<string> inputImage;

        private string _messageState = "";

        protected override async Task OnInitializedAsync()
        {
            try
            {

                if (Id != null)
                {
                   

                    _logo = await RestClientService.GetItem<Logo, int>(Id.Value, ConstHelper.LogosPath); 
                }
                else
                    _logo = new Logo();
                
               
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
                var resp =  await RestClientService.Post<Logo, int>(_logo, ConstHelper.LogosPath);

                if (resp.State)
                    NavigationManager.NavigateTo("/Settings/Logos/Index");
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
            NavigationManager.NavigateTo("/Settings/Logos/Index");
        }

        void Change(string value, string name)
        {
            
        }

        void Error(UploadErrorEventArgs args, string name)
        {
            
        }

    }
}
