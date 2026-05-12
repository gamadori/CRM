using CRM.Client.Helpers;
using CRM.Client.Shared.Components;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace CRM.Client.Pages.DocViewer
{
   
    public partial class Index: ComponentBase
    {
        [Inject]
        HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int Id { get; set; }

        private Attachment _attachment = null;

        private bool _notFound = false;

        private bool _loading = false;
        protected override async Task OnInitializedAsync()
        {
            await LoadAttachment();
        }

        protected async Task LoadAttachment()
        {
            string path;
            try
            {
                _loading = true;
                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.AttachmentsPath;

                path += $"/{Id}";

                _attachment = await Http.GetFromJsonAsync<Attachment>(path);
                _notFound = _attachment == null;
                _loading = false;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
