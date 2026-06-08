using Microsoft.Extensions.Logging;
using ProtonDrive.App.Settings.Remote;
using ProtonDrive.Client;
using ProtonDrive.Client.Instrumentation.Telemetry;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.Diagnostics.Metrics.Thumbnails;

namespace ProtonDrive.App.Instrumentation.Telemetry.ThumbnailGeneration;

internal sealed class ThumbnailGenerationReportingService : IRemoteSettingsAware
{
    private readonly IThumbnailGenerationMetricsCollector _metricsCollector;
    private readonly ITelemetryApiClient _telemetryApiClient;
    private readonly ILogger<ThumbnailGenerationReportingService> _logger;
    private readonly CancellationHandle _cancellationHandle = new();
    private readonly TimeSpan _period;

    private PeriodicTimer _timer;
    private Task? _timerTask;

    public ThumbnailGenerationReportingService(
        AppConfig appConfig,
        IThumbnailGenerationMetricsCollector metricsCollector,
        ITelemetryApiClient telemetryApiClient,
        ILogger<ThumbnailGenerationReportingService> logger)
    {
        _metricsCollector = metricsCollector;
        _telemetryApiClient = telemetryApiClient;
        _logger = logger;

        _period = appConfig.PeriodicThumbnailGenerationReportInterval.RandomizedWithDeviation(0.2);
        _timer = new PeriodicTimer(_period);
    }

    void IRemoteSettingsAware.OnRemoteSettingsChanged(RemoteSettings settings)
    {
        if (settings.IsTelemetryEnabled)
        {
            Start();
        }
        else
        {
            Stop();
        }
    }

    private void Start()
    {
        if (_timerTask is not null)
        {
            return;
        }

        _timer = new PeriodicTimer(_period);
        _timerTask = ReportPeriodicallyAsync(_cancellationHandle.Token);
        _metricsCollector.Enable();
    }

    private void Stop()
    {
        if (_timerTask is null)
        {
            return;
        }

        _metricsCollector.Disable();
        _cancellationHandle.Cancel();
        _timerTask = null;
        _timer.Dispose();
    }

    private async Task ReportPeriodicallyAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var statistics = _metricsCollector.GetAndReset();

                    if (statistics.Count == 0)
                    {
                        continue;
                    }

                    var events = ThumbnailGenerationReportFactory.CreateReport(statistics).ToList();

                    await _telemetryApiClient
                        .SendEventsAsync(new TelemetryEvents(events), cancellationToken)
                        .ThrowOnFailure()
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to send thumbnail generation telemetry report: {Message}", ex.CombinedMessage());
                }
            }
        }
        catch (OperationCanceledException)
        {
            /* Do nothing */
        }
    }
}
