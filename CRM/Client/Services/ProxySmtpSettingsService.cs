using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace CRM.Client.Services
{
    public class ProxySmtpSettingsService: ProxyRestClientService<SmtpSetting>, ISmtpSettingsService
    {
        
        public ProxySmtpSettingsService(HttpClient http) : base(http, ConstHelper.SmtpSettingsPath)
        {

        }

    }
}
