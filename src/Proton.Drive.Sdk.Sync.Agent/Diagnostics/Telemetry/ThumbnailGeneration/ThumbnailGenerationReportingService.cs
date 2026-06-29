using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Settings.Remote;
using Proton.Drive.Sdk.Sync.Client;
using Proton.Drive.Sdk.Sync.Client.Instrumentation.Telemetry;
using Proton.Drive.Sdk.Sync.Shared.Diagnostics.Metrics.Thumbnails;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.ThumbnailGeneration;

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
