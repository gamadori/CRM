namespace CRM.Server.Services
{
    public sealed class MaintenanceAppOfflineBackgroundService : BackgroundService
    {
        private readonly MaintenanceState _maintenanceState;
        private readonly IAppOfflineService _appOfflineService;
        private readonly ILogger<MaintenanceAppOfflineBackgroundService> _logger;

        public MaintenanceAppOfflineBackgroundService(
            MaintenanceState maintenanceState,
            IAppOfflineService appOfflineService,
            ILogger<MaintenanceAppOfflineBackgroundService> logger)
        {
            _maintenanceState = maintenanceState;
            _appOfflineService = appOfflineService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var notice = _maintenanceState.GetCurrent();
                if (!notice.Active ||
                    !notice.AutoPublishAppOffline ||
                    notice.StartsAtUtc is null ||
                    notice.AppOfflinePublishedAtUtc is not null ||
                    notice.StartsAtUtc > DateTimeOffset.UtcNow)
                {
                    continue;
                }

                try
                {
                    await _appOfflineService.PublishAsync(stoppingToken);
                    _maintenanceState.MarkAppOfflinePublished();
                    _logger.LogWarning("app_offline.htm published automatically for scheduled maintenance.");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to publish app_offline.htm for scheduled maintenance.");
                }
            }
        }
    }
}
