using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRM.Server.Services.Sms
{
    /// <summary>
    /// Invio SMS tramite l'API REST di Twilio usando solo <see cref="HttpClient"/>
    /// (nessun SDK aggiuntivo): così sostituire il provider non richiede dipendenze.
    /// Registrato come typed client via <c>AddHttpClient</c>.
    /// </summary>
    public class TwilioSmsSender : ISmsSender
    {
        private readonly HttpClient _http;
        private readonly TwilioOptions _options;
        private readonly ILogger<TwilioSmsSender> _logger;

        public TwilioSmsSender(HttpClient http, IOptions<SmsOptions> options, ILogger<TwilioSmsSender> logger)
        {
            _http = http;
            _options = options.Value.Twilio;
            _logger = logger;

            _http.BaseAddress ??= new Uri("https://api.twilio.com/");
            if (IsConfigured)
            {
                var basic = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{_options.AccountSid}:{_options.AuthToken}"));
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
            }
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_options.AccountSid) &&
            !string.IsNullOrWhiteSpace(_options.AuthToken) &&
            (!string.IsNullOrWhiteSpace(_options.From) || !string.IsNullOrWhiteSpace(_options.MessagingServiceSid));

        public async Task<bool> SendAsync(string toPhoneE164, string text, CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("TwilioSmsSender: credenziali/mittente non configurati.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(toPhoneE164))
                return false;

            var form = new List<KeyValuePair<string, string>>
            {
                new("To", toPhoneE164),
                new("Body", text)
            };

            // MessagingServiceSid (se presente) ha priorità sul numero mittente.
            if (!string.IsNullOrWhiteSpace(_options.MessagingServiceSid))
                form.Add(new("MessagingServiceSid", _options.MessagingServiceSid));
            else
                form.Add(new("From", _options.From));

            try
            {
                using var content = new FormUrlEncodedContent(form);
                var url = $"2010-04-01/Accounts/{_options.AccountSid}/Messages.json";

                using var response = await _http.PostAsync(url, content, ct);
                if (response.IsSuccessStatusCode)
                    return true;

                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Invio SMS Twilio fallito ({Status}): {Body}", (int)response.StatusCode, body);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'invio SMS Twilio a {Phone}.", toPhoneE164);
                return false;
            }
        }
    }
}
