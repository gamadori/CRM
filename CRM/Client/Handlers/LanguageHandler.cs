using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CRM.Client.Handlers
{
    public class LanguageHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Usa la cultura corrente impostata da Blazor
            var language = CultureInfo.CurrentUICulture.Name;

            // DEBUG: Log per verificare quale lingua viene inviata
            System.Diagnostics.Debug.WriteLine($"[LanguageHandler] Sending Accept-Language: {language}");

            request.Headers.AcceptLanguage.Clear();
            request.Headers.AcceptLanguage.Add(
                new System.Net.Http.Headers.StringWithQualityHeaderValue(language));

            return base.SendAsync(request, cancellationToken);
        }
    }
}
