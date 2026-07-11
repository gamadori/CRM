using CRM.Client.Services;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    public class WorkflowAutomationBackgroundService : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);
        private const int BatchSize = 50;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WorkflowAutomationBackgroundService> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public WorkflowAutomationBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<WorkflowAutomationBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunCycleAsync(stoppingToken);

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task RunCycleAsync(CancellationToken stoppingToken)
        {
            if (!await _lock.WaitAsync(0, stoppingToken))
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IWorkflowAutomationService>();
                var executed = await service.ExecutePendingAsync(BatchSize, stoppingToken);

                if (executed > 0)
                {
                    _logger.LogInformation("WorkflowAutomationBackgroundService: eseguite {Count} automazioni", executed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WorkflowAutomationBackgroundService: errore nel ciclo");

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var log = scope.ServiceProvider.GetRequiredService<ILogEventService>();
                    await log.RegisterAsync(nameof(WorkflowAutomationBackgroundService), nameof(RunCycleAsync), EventsTypes.Error, ex);
                }
                catch
                {
                }
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
