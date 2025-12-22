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

namespace CRM.Client.Pages.Settings.EmailTemplates
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        
        [Inject]
        IAGRestClientService RestClientService { get; set; }

       

        [Parameter]
        public int? Id { get; set; }

        private EmailTemplate _template = null;

        private List<Logo> _loghi;

        private string _messageState = null;
        protected override async Task OnInitializedAsync()
        {
           
            try
            {


                if (Id != null)
                {

                    _template = await RestClientService.GetItem<EmailTemplate, int>(Id.Value, ConstHelper.EmailTemplatePath);

                    //_template = await _service.Get(Id.Value);
                }
                else
                    _template = new EmailTemplate();

                await LoadLoghi();
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
                var resp = await RestClientService.Post<EmailTemplate, int>(_template, ConstHelper.EmailTemplatePath);

                if (resp != null && resp.State)
                
                    NavigationManager.NavigateTo("/Settings/EmailTemplates");
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
            NavigationManager.NavigateTo("/Settings/EmailTemplates/Index");
        }

        void Change(string value, string name)
        {

        }

        void Error(UploadErrorEventArgs args, string name)
        {

        }

        private List<object> EnumGetList()
        {
            var list = new List<object>();

            Array values = System.Enum.GetValues(typeof(EmailsTypes));
            foreach (EmailsTypes value in values)
                list.Add(new { Value = value, Text = Enum.GetName(typeof(EmailsTypes), value) });

            return list;
        
        }

        private async Task LoadLoghi()
        {
            var loghi = await RestClientService.Get<Logo, LogosFilterModel>(new LogosFilterModel(), ConstHelper.LogosPath);
            _loghi = loghi.Items;

        }
    }
}
