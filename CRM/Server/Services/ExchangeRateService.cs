using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    /// <summary>
    /// Cambi di riferimento BCE, esposti dal servizio pubblico Frankfurter (nessuna chiave).
    /// <para>
    /// Il tasso restituito e' una PROPOSTA: chi registra la spesa puo' sempre sostituirlo, perche'
    /// il cambio corretto per un rimborso e' spesso quello applicato dalla carta e non quello di
    /// riferimento. Quale dei due usare e' una decisione contabile, non tecnica.
    /// </para>
    /// <para>
    /// Nessuna eccezione esce da qui: un cambio non disponibile non deve impedire di registrare
    /// una spesa. Meglio una spesa registrata e da convertire che una spesa persa.
    /// </para>
    /// </summary>
    public class ExchangeRateService : IExchangeRateService
    {
        private const string BaseUrl = "https://api.frankfurter.dev/v1";

        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ExchangeRateService> _logger;

        public ExchangeRateService(HttpClient http, IMemoryCache cache, ILogger<ExchangeRateService> logger)
        {
            _http = http;
            _cache = cache;
            _logger = logger;

            // Una spesa non si salva piu' lentamente perche' un servizio esterno e' lento.
            _http.Timeout = TimeSpan.FromSeconds(8);
        }

        public async Task<decimal?> GetRateAsync(string fromCurrency, string toCurrency, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(fromCurrency) || string.IsNullOrWhiteSpace(toCurrency))
                return null;

            var from = fromCurrency.Trim().ToUpperInvariant();
            var to = toCurrency.Trim().ToUpperInvariant();

            // Stessa valuta: il cambio e' 1 e non si disturba nessun servizio.
            if (from == to)
                return 1m;

            // Il cambio di una data passata non cambia piu': si puo' tenere in cache a lungo.
            // Per la data di oggi la BCE pubblica una volta al giorno, quindi un'ora e' prudente.
            var day = date.Date;
            var cacheKey = $"fx:{from}:{to}:{day:yyyy-MM-dd}";

            if (_cache.TryGetValue<decimal?>(cacheKey, out var cached))
                return cached;

            var rate = await FetchRateAsync(from, to, day);

            var ttl = day < DateTime.Today ? TimeSpan.FromDays(30) : TimeSpan.FromHours(1);

            // Anche l'assenza di risposta viene messa in cache, ma per poco: senza, ogni
            // salvataggio su una valuta non coperta ritenterebbe la chiamata.
            _cache.Set(cacheKey, rate, rate.HasValue ? ttl : TimeSpan.FromMinutes(10));

            return rate;
        }

        private async Task<decimal?> FetchRateAsync(string from, string to, DateTime day)
        {
            // Forma diretta "base=<valuta spesa>&symbols=<valuta base>": il numero che torna e'
            // gia' il moltiplicatore da applicare all'importo, senza divisioni da interpretare.
            var url = $"{BaseUrl}/{day:yyyy-MM-dd}?base={from}&symbols={to}";

            try
            {
                using var response = await _http.GetAsync(url);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // La BCE non copre ogni valuta del mondo: e' un esito previsto, non un guasto.
                    _logger.LogInformation("Cambio {From}->{To} non disponibile al {Day:yyyy-MM-dd}: valuta non coperta.", from, to, day);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Servizio cambi ha risposto {Status} per {From}->{To} al {Day:yyyy-MM-dd}.",
                        (int)response.StatusCode, from, to, day);
                    return null;
                }

                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = payload.RootElement;

                if (!root.TryGetProperty("rates", out var rates)
                    || !rates.TryGetProperty(to, out var value)
                    || !value.TryGetDecimal(out var rate)
                    || rate <= 0)
                {
                    _logger.LogWarning("Risposta del servizio cambi priva del tasso {To} per {From} al {Day:yyyy-MM-dd}.", to, from, day);
                    return null;
                }

                // Nei giorni di chiusura la BCE non pubblica e il servizio risponde con l'ultima
                // quotazione utile: si registra quale, perche' non e' la data richiesta.
                if (root.TryGetProperty("date", out var effective)
                    && DateTime.TryParse(effective.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var effectiveDay)
                    && effectiveDay.Date != day)
                {
                    _logger.LogInformation("Cambio {From}->{To}: richiesto il {Day:yyyy-MM-dd}, usata la quotazione del {Effective:yyyy-MM-dd}.",
                        from, to, day, effectiveDay);
                }

                return rate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Servizio cambi non raggiungibile per {From}->{To} al {Day:yyyy-MM-dd}.", from, to, day);
                return null;
            }
        }
    }
}
