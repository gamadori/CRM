using CRM.Client.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;

namespace CRM.Client.Pages.About
{
    [Authorize]
    public partial class Index: ComponentBase
    {
        [Inject]
        HttpClient Client { get; set; }

        private AboutModel? _data = null;
        private AboutModel? _dataClient = null;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
            await base.OnInitializedAsync();
        }

        private async Task LoadData()
        {
            _data = await Client.GetFromJsonAsync<AboutModel>(ConstHelper.AboutPath);
            _dataClient = new AboutModel();
            var asm = Assembly.GetExecutingAssembly();
            _dataClient.Name = asm.GetName().Name;
            _dataClient.Version = asm.GetName().Version.ToString();
            _dataClient.Date = _data.Date;
            _dataClient.Description = "CRM Client";
            StateHasChanged();
          
        }


    }
}
