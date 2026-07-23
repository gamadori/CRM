using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public sealed class MaintenanceState
    {
        private readonly object _sync = new();
        private MaintenanceNoticeDTO _current = new();

        public MaintenanceNoticeDTO GetCurrent()
        {
            lock (_sync)
                return Copy(_current);
        }

        public MaintenanceNoticeDTO Schedule(int minutes, string? message, bool autoPublishAppOffline)
        {
            lock (_sync)
            {
                _current = new MaintenanceNoticeDTO
                {
                    Active = true,
                    StartsAtUtc = DateTimeOffset.UtcNow.AddMinutes(minutes),
                    Message = string.IsNullOrWhiteSpace(message)
                        ? "Il server sarà temporaneamente non disponibile per manutenzione."
                        : message.Trim(),
                    AutoPublishAppOffline = autoPublishAppOffline
                };
                return Copy(_current);
            }
        }

        public MaintenanceNoticeDTO Cancel()
        {
            lock (_sync)
            {
                _current = new MaintenanceNoticeDTO();
                return Copy(_current);
            }
        }

        public MaintenanceNoticeDTO MarkAppOfflinePublished()
        {
            lock (_sync)
            {
                _current.AppOfflinePublishedAtUtc = DateTimeOffset.UtcNow;
                return Copy(_current);
            }
        }

        private static MaintenanceNoticeDTO Copy(MaintenanceNoticeDTO notice) => new()
        {
            Active = notice.Active,
            StartsAtUtc = notice.StartsAtUtc,
            Message = notice.Message,
            AutoPublishAppOffline = notice.AutoPublishAppOffline,
            AppOfflinePublishedAtUtc = notice.AppOfflinePublishedAtUtc
        };
    }
}
