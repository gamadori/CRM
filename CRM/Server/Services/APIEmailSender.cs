using CRM.Server.Models;

namespace CRM.Server.Services
{
    public class APIEmailSender: IAPIEmailSender
    {
        private readonly IAPIEmailSender _primary;
        private readonly IAPIEmailSender _fallback;
        private readonly ILogEventService _logEventService;

        public APIEmailSender(
            IAPIEmailSender primary,
            IAPIEmailSender fallback,
            ILogEventService logger)
        {
            _primary = primary;
            _fallback = fallback;
            _logEventService = logger;
        }

        public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            try
            {
                await _primary.SendAsync(message, ct);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(APIEmailSender), nameof(SendAsync), Shared.LogEvent.EventsTypes.Error, ex);

                try
                {
                    await _fallback.SendAsync(message, ct);
                }
                catch (Exception exfallback)
                {
                    await _logEventService.RegisterAsync(nameof(APIEmailSender), nameof(SendAsync), Shared.LogEvent.EventsTypes.Error, exfallback);
                    throw; // qui decidi tu: log-only o eccezione
                }
            }
        }
    }
}
