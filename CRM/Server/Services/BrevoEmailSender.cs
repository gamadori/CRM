using CRM.Server.Models;

namespace CRM.Server.Services
{
    public class BrevoEmailSender : IAPIEmailSender
    {
        private readonly HttpClient _http;
        private readonly string _from;

        public BrevoEmailSender(HttpClient http, string apiKey, string from)
        {
            _http = http;
            _from = from;

            _http.BaseAddress = new Uri("https://api.brevo.com/v3/");
            _http.DefaultRequestHeaders.Add("api-key", apiKey);
        }

        public async Task SendAsync(EmailMessage msg, CancellationToken ct = default)
        {
            var payload = new
            {
                sender = new { email = _from },
                to = new[] { new { email = msg.To } },
                subject = msg.Subject,
                htmlContent = msg.HtmlBody,
                textContent = msg.TextBody
            };

            var res = await _http.PostAsJsonAsync("smtp/email", payload, ct);

            if (!res.IsSuccessStatusCode)
            {
                throw new Exception($"Brevo failed: {res.StatusCode}");
            }
        }
    }
}
