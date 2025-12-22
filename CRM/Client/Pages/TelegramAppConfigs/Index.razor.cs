using CRM.Client.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace CRM.Client.Pages.TelegramAppConfigs
{
    public partial class Index: ComponentBase
    {
        [Inject]
        HttpClient HttpClient { get; set; }


        private TelegramStatus _status = null;

        private string _configNeed = string.Empty;
        private System.Threading.Timer? _timer; // NOTE: THIS LINE OF CODE ADDED


        protected override async Task OnInitializedAsync()
        {
            await GetState();

            _timer = new System.Threading.Timer(async (object? stateInfo) =>
            {
                await GetState();
                StateHasChanged(); 
            }, new System.Threading.AutoResetEvent(false), 2000, 2000); // fire every 2000 milliseconds
        }

        private async Task GetState()
        {
            _status = await HttpClient.GetFromJsonAsync<TelegramStatus>($"{ConstHelper.UserBotPath}/status");



        }

        private async Task SetConfig()
        {
            string url = $"{ConstHelper.UserBotPath}/config?value={_configNeed}";
            await HttpClient.GetAsync(url);

            await GetState();
        }
    }
}
