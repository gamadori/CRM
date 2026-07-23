using System;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared.DTOs
{
    public sealed class MaintenanceNoticeDTO
    {
        public bool Active { get; set; }
        public DateTimeOffset? StartsAtUtc { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool AutoPublishAppOffline { get; set; }
        public DateTimeOffset? AppOfflinePublishedAtUtc { get; set; }
    }

    public sealed class MaintenanceStatusDTO
    {
        public MaintenanceNoticeDTO Notice { get; set; } = new();
        public int ConnectedUsers { get; set; }
        public int ConnectedConnections { get; set; }
        public bool AppOfflineFileExists { get; set; }
    }

    public sealed class ScheduleMaintenanceRequest
    {
        [Range(1, 1440)]
        public int Minutes { get; set; } = 10;

        [MaxLength(500)]
        public string Message { get; set; } = "Il server sarà temporaneamente non disponibile per manutenzione.";
        public bool AutoPublishAppOffline { get; set; }
    }
}
