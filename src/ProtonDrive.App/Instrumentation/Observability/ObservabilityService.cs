using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Account;
using ProtonDrive.App.Instrumentation.Observability.TransferPerformance;
using ProtonDrive.App.Instrumentation.Telemetry.Synchronization;
using ProtonDrive.App.Services;
using ProtonDrive.App.Settings.Remote;
using ProtonDrive.Client;
using ProtonDrive.Client.Instrumentation.Observability;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Instrumentation.Observability;

internal sealed class ObservabilityService : IUserStateAware, IRemoteSettingsAware, IStoppableService
{
    private readonly IClock _clock;
    private readonly IObservabilityApiClient _observabilityApiClient;
    private readonly GenericTransferPerformanceMetricsFactory _genericTransferPerformanceMetricsFactory;
    private readonly IMetricsService _driveClientMetricsService;
    private readonly ILogger<ObservabilityService> _logger;

    private readonly CancellationHandle _cancellationHandle = new();
    private readonly TimeSpan _transferPerformanceReportInterval;
    private readonly TimeSpan _period;
    private readonly SerialScheduler _scheduler = new();

    private PeriodicTimer _timer;
    private Task? _timerTask;
    private TickCount _nextTransferPerformanceReportTime;
    private bool _userHasAPaidPlan;

    public ObservabilityService(
        AppConfig appConfig,
        IClock clock,
        IObservabilityApiClient observabilityApiClient,
        GenericTransferPerformanceMetricsFactory genericTransferPerformanceMetricsFactory,
        IMetricsService driveClientMetricsService,
        ILogger<ObservabilityService> logger)
    {
        _clock = clock;
        _observabilityApiClient = observabilityApiClient;
        _genericTransferPerformanceMetricsFactory = genericTransferPerformanceMetricsFactory;
        _driveClientMetricsService = driveClientMetricsService;
        _logger = logger;

        _transferPerformanceReportInterval = appConfig.PeriodicTransferPerformanceReportInterval;
        _period = appConfig.PeriodicObservabilityReportInterval.RandomizedWithDeviation(0.2);
        _timer = new PeriodicTimer(_period);
    }

    void IUserStateAware.OnUserStateChanged(UserState value)
    {
        _userHasAPaidPlan = value.SubscriptionPlanCode is not null && value.SubscriptionPlanCode != PeriodicReportConstants.FreePlan;
    }

    void IRemoteSettingsAware.OnRemoteSettingsChanged(RemoteSettings settings)
    {
        if (settings.IsTelemetryEnabled)
        {
            Schedule(Start);
        }
        else
        {
            Schedule(Stop);
        }
    }

    Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        Schedule(Stop);
        return WaitForCompletionAsync();
    }

    internal Task WaitForCompletionAsync()
    {
        // Wait for all scheduled tasks to complete
        return Schedule(() => { });
    }

    private void Start()
    {
        if (_timerTask is not null)
        {
            return;
        }

        Clear();
        _driveClientMetricsService.Start();

        _nextTransferPerformanceReportTime = _clock.TickCount + _transferPerformanceReportInterval;
        _timer = new PeriodicTimer(_period);
        _timerTask = PeriodicallySendMetricsAsync(_cancellationHandle.Token);
    }

    private void Stop()
    {
        if (_timerTask is null)
        {
            return;
        }

        _driveClientMetricsService.Stop();

        _cancellationHandle.Cancel();
        _timerTask = null;
        _timer.Dispose();
    }

    private async Task PeriodicallySendMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await Schedule(SendMetricsAsync, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            /* Do nothing */
        }
    }

    private async Task SendMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var metrics = GetMetrics();

            if (metrics.Metrics.Count > 0)
            {
                await _observabilityApiClient.SendMetricsAsync(metrics, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to send observability metrics: {ErrorCode} : {ErrorMessage}", ex.GetRelevantFormattedErrorCode(), ex.Message);
        }
    }

    private ObservabilityMetrics GetMetrics()
    {
        var driveClientMetrics = _driveClientMetricsService.GetMetrics(_userHasAPaidPlan);
        var performanceMetrics = GetTransferPerformanceMetrics();

        var metrics = Enumerable.Empty<ObservabilityMetric>()
            .Concat(performanceMetrics)
            .Concat(driveClientMetrics)
            .ToList();

        return new ObservabilityMetrics(metrics);
    }

    private void Clear()
    {
        _genericTransferPerformanceMetricsFactory.Clear();
    }

    private ImmutableList<ObservabilityMetric> GetTransferPerformanceMetrics()
    {
        var now = _clock.TickCount;

        // Transfer performance is reported less often than other metrics
        if (now < _nextTransferPerformanceReportTime)
        {
            return [];
        }

        _nextTransferPerformanceReportTime = now + _transferPerformanceReportInterval;

        return _genericTransferPerformanceMetricsFactory.GetMetrics();
    }

    private Task Schedule(Action action)
    {
        return _scheduler.Schedule(action);
    }

    private Task Schedule(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        return _scheduler.Schedule(action, cancellationToken);
    }
}
