using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Settings.Smtp
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
        [Inject]
        ISettingsService<SmtpSettings> _service { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

       

        private bool _saving = false;
      
        private SmtpSettings _smtp = null;


        protected override async Task OnInitializedAsync()
        {
            
            try
            {

                
                _smtp = await _service.Get();
                
                  
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                if (_smtp == null)
                    _smtp = new SmtpSettings();
            }
        }

        protected async Task HandleValidSubmit()
        {
            bool resp;

            try
            {
                _saving = true;
                StateHasChanged();

                resp = await _service.Post(_smtp);

                
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            finally
            {
                _saving = false;
                NavigationManager.NavigateTo("/Settings");
            }
        }

        protected void Annulla()
        {
            NavigationManager.NavigateTo("/Settings");
        }

        protected void Delete()
        {

        }

    }
}
