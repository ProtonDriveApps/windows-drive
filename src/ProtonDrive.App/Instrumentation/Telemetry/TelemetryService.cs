using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ProtonDrive.App.Account;
using ProtonDrive.App.Instrumentation.Telemetry.Errors;
using ProtonDrive.App.Instrumentation.Telemetry.FileIntegrity;
using ProtonDrive.App.Instrumentation.Telemetry.FirstLaunch;
using ProtonDrive.App.Instrumentation.Telemetry.MappingSetup;
using ProtonDrive.App.Instrumentation.Telemetry.Synchronization;
using ProtonDrive.App.Settings.Remote;
using ProtonDrive.Client;
using ProtonDrive.Client.Instrumentation.Telemetry;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Instrumentation.Telemetry;

internal sealed class TelemetryService : IRemoteSettingsAware, IUserStateAware
{
    private readonly CancellationHandle _cancellationHandle = new();
    private readonly SyncStatistics _syncStatistics;
    private readonly MappingSetupStatistics _mappingStatistics;
    private readonly FileIntegrityStatistics _fileIntegrityStatistics;
    private readonly SharedWithMeItemCounters _sharedWithMeItemCounters;
    private readonly OpenedDocumentsCounters _openedDocumentsCounters;
    private readonly IErrorCounter _errorCounter;
    private readonly IErrorCountProvider _errorCountProvider;
    private readonly TimeSpan _period;
    private readonly ITelemetryApiClient _telemetryApiClient;
    private readonly ILogger<TelemetryService> _logger;
    private readonly Lazy<Task> _reportFirstLaunchTask;

    private PeriodicTimer _timer;
    private Task? _timerTask;
    private bool? _userHasAPaidPlan;

    public TelemetryService(
        AppConfig appConfig,
        SyncStatistics syncStatistics,
        MappingSetupStatistics mappingStatistics,
        FileIntegrityStatistics fileIntegrityStatistics,
        SharedWithMeItemCounters sharedWithMeItemCounters,
        OpenedDocumentsCounters openedDocumentsCounters,
        IErrorCounter errorCounter,
        IErrorCountProvider errorCountProvider,
        ITelemetryApiClient telemetryApiClient,
        ILogger<TelemetryService> logger)
    {
        _syncStatistics = syncStatistics;
        _mappingStatistics = mappingStatistics;
        _fileIntegrityStatistics = fileIntegrityStatistics;
        _sharedWithMeItemCounters = sharedWithMeItemCounters;
        _openedDocumentsCounters = openedDocumentsCounters;
        _errorCounter = errorCounter;
        _errorCountProvider = errorCountProvider;
        _telemetryApiClient = telemetryApiClient;
        _logger = logger;

        _period = appConfig.PeriodicTelemetryReportInterval.RandomizedWithDeviation(0.2);
        _timer = new PeriodicTimer(_period);
        _reportFirstLaunchTask = new Lazy<Task>(ReportFirstLaunchAsync);
    }

    void IUserStateAware.OnUserStateChanged(UserState value)
    {
        if (value.SubscriptionPlanCode is not null)
        {
            _userHasAPaidPlan = value.SubscriptionPlanCode != PeriodicReportConstants.FreePlan;
        }
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

    private async Task ReportFirstLaunchAsync()
    {
        const string protonDriveRegistryKeyName = @"Software\Proton\Drive";
        const string sourceRegistryValueName = "InstallationInitiator";
        const string reportRegistryValueName = "FirstLaunch";

        try
        {
            using RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(protonDriveRegistryKeyName, writable: true);

            if (registryKey is null)
            {
                return;
            }

            if (!FirstLaunchReportMustBeSent(registryKey, reportRegistryValueName))
            {
                return;
            }

            var initiatorValue = registryKey.GetValue(sourceRegistryValueName);

            if (initiatorValue is not string initiator || string.IsNullOrWhiteSpace(initiator))
            {
                initiator = "own";
            }

            var installationSourceEvent = FirstLaunchReportFactory.CreateEvent(initiator);

            await _telemetryApiClient.SendEventAsync(installationSourceEvent, _cancellationHandle.Token).ConfigureAwait(false);

            // Mark the installation source report as sent by setting the registry value to 1.
            // This ensures that the report is not sent multiple times during the lifetime of the application instance.
            registryKey.SetValue(reportRegistryValueName, 1, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to report installation source: {Message}", ex.Message);
        }

        return;

        static bool FirstLaunchReportMustBeSent(RegistryKey registryKey, string value)
        {
            return registryKey.GetValue(value) is 0;
        }
    }

    private void Start()
    {
        if (_timerTask is not null)
        {
            return; // Task already started
        }

        _timer = new PeriodicTimer(_period);
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
        _timer.Dispose();
    }

    private async Task ReportStatisticsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _reportFirstLaunchTask.Value.ConfigureAwait(false);

            while (await _timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var syncReport = new List<TelemetryEvent>(1)
                    {
                        SynchronizationReportFactory.CreateReport(_syncStatistics, _userHasAPaidPlan, _sharedWithMeItemCounters, _openedDocumentsCounters),
                    };

                    var errorReport = ErrorReportFactory.CreateReport(_errorCountProvider.GetTopErrorCounts(maximumNumberOfCounters: 10));

                    var mappingReport = MappingSetupReportFactory.CreateReport(_mappingStatistics.GetMappingDetails());

                    var fileIntegrityReport = FileIntegrityReportFactory.CreateReport(_fileIntegrityStatistics.GetDetails());

                    var fileIntegrityStatusReport = FileIntegrityReportFactory.CreateStatusReport(_fileIntegrityStatistics.GetOverallStatus());

                    var telemetryEvents = new TelemetryEvents(
                        [.. syncReport, .. errorReport, .. mappingReport, .. fileIntegrityReport, .. fileIntegrityStatusReport]);

                    if (telemetryEvents.Events.Count == 0)
                    {
                        continue; // Nothing to report
                    }

                    await _telemetryApiClient.SendEventsAsync(telemetryEvents, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to send the periodic reports: {Message}", ex.CombinedMessage());
                }
                finally
                {
                    // Sync report
                    _syncStatistics.Reset();
                    _sharedWithMeItemCounters.Reset();
                    _openedDocumentsCounters.Reset();

                    // Error report
                    _errorCounter.Reset();
                }
            }
        }
        catch (OperationCanceledException)
        {
            /* Do nothing */
        }
    }
}
