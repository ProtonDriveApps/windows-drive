using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Settings.Remote;
using Proton.Drive.Sdk.Sync.Client;
using Proton.Drive.Sdk.Sync.Client.Instrumentation.Telemetry;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Telemetry;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry;

internal sealed class TelemetryService : IRemoteSettingsAware
{
    private readonly ITelemetryApiClient _telemetryApiClient;
    private readonly IEnumerable<IOneTimeTelemetryReportProvider> _oneTimeReportProviders;
    private readonly IEnumerable<IPeriodicTelemetryReportProvider> _periodicReportProviders;
    private readonly Func<TimeSpan, IPeriodicTimer> _periodicTimerFactory;
    private readonly ILogger<TelemetryService> _logger;

    private readonly CancellationHandle _cancellationHandle = new();
    private readonly TimeSpan _period;
    private readonly TimeSpan _startupDelay;

    private IPeriodicTimer? _timer;
    private Task? _timerTask;

    public TelemetryService(
        AppConfig appConfig,
        ITelemetryApiClient telemetryApiClient,
        IEnumerable<IOneTimeTelemetryReportProvider> oneTimeReportProviders,
        IEnumerable<IPeriodicTelemetryReportProvider> periodicReportProviders,
        Func<TimeSpan, IPeriodicTimer> periodicTimerFactory,
        ILogger<TelemetryService> logger)
    {
        _telemetryApiClient = telemetryApiClient;
        _oneTimeReportProviders = oneTimeReportProviders;
        _periodicReportProviders = periodicReportProviders;
        _periodicTimerFactory = periodicTimerFactory;
        _logger = logger;

        _period = appConfig.PeriodicTelemetryReportInterval.RandomizedWithDeviation(0.1, JitterDirection.PositiveOnly);
        _startupDelay = _period / 5;
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
            return; // Task already started
        }

        Clear();

        _timerTask = ReportStatisticsAsync(_cancellationHandle.Token);
    }

    private void Stop()
    {
        if (_timerTask is null)
        {
            return;
        }

        _cancellationHandle.Cancel();
        _timerTask = null;
        _timer?.Dispose();
    }

    private async Task ReportStatisticsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _timer = _periodicTimerFactory.Invoke(_startupDelay);
            await _timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);

            _timer.Dispose();
            _timer = _periodicTimerFactory.Invoke(_period);

            if (await SendReportsAsync(GetOneTimeReports, cancellationToken).ConfigureAwait(false))
            {
                OnOneTimeReportsSent();
            }

            while (await _timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await SendReportsAsync(GetPeriodicReports, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            /* Do nothing */
        }
    }

    private IEnumerable<TelemetryEvent> GetOneTimeReports()
    {
        return _oneTimeReportProviders.SelectMany(reportProvider => reportProvider.GetOneTimeReport());
    }

    private void OnOneTimeReportsSent()
    {
        foreach (var provider in _oneTimeReportProviders)
        {
            provider.OnOneTimeReportSent();
        }
    }

    private IEnumerable<TelemetryEvent> GetPeriodicReports()
    {
        return _periodicReportProviders.SelectMany(reportProvider => reportProvider.GetPeriodicReport());
    }

    private async Task<bool> SendReportsAsync(Func<IEnumerable<TelemetryEvent>> reportProvider, CancellationToken cancellationToken)
    {
        try
        {
            var reports = reportProvider.Invoke().ToList();
            var telemetryEvents = new TelemetryEvents(reports);

            if (telemetryEvents.Events.Count != 0)
            {
                await _telemetryApiClient.SendEventsAsync(telemetryEvents, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to send telemetry reports: {Message}", ex.CombinedMessage());
        }

        return false;
    }

    private void Clear()
    {
        foreach (var telemetryEventProvider in _periodicReportProviders)
        {
            telemetryEventProvider.Reset();
        }
    }
}
