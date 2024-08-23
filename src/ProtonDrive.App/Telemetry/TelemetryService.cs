using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Account;
using ProtonDrive.App.Settings.Remote;
using ProtonDrive.Client;
using ProtonDrive.Client.Telemetry;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Telemetry;

internal sealed class TelemetryService : IRemoteSettingsStateAware, IUserStateAware
{
    private readonly CancellationHandle _cancellationHandle = new();
    private readonly SyncStatistics _statistics;
    private readonly SharedWithMeItemCounters _sharedWithMeItemCounters;
    private readonly OpenedDocumentsCounters _openedDocumentsCounters;
    private readonly IErrorCounter _errorCounter;
    private readonly IErrorCountProvider _errorCountProvider;
    private readonly TimeSpan _period;
    private readonly ITelemetryApiClient _telemetryApiClient;
    private readonly ILogger<TelemetryService> _logger;

    private PeriodicTimer _timer;
    private Task? _timerTask;
    private bool? _userHasAPaidPlan;

    public TelemetryService(
        AppConfig appConfig,
        SyncStatistics statistics,
        SharedWithMeItemCounters sharedWithMeItemCounters,
        OpenedDocumentsCounters openedDocumentsCounters,
        IErrorCounter errorCounter,
        IErrorCountProvider errorCountProvider,
        ITelemetryApiClient telemetryApiClient,
        ILogger<TelemetryService> logger)
    {
        _telemetryApiClient = telemetryApiClient;
        _logger = logger;
        _statistics = statistics;
        _sharedWithMeItemCounters = sharedWithMeItemCounters;
        _openedDocumentsCounters = openedDocumentsCounters;
        _errorCounter = errorCounter;
        _errorCountProvider = errorCountProvider;

        _period = appConfig.PeriodicTelemetryReportInterval.RandomizedWithDeviation(0.2);
        _timer = new PeriodicTimer(_period);
    }

    void IUserStateAware.OnUserStateChanged(UserState value)
    {
        if (value.SubscriptionPlanCode is not null)
        {
            _userHasAPaidPlan = value.SubscriptionPlanCode != PeriodicReportConstants.FreePlan;
        }
    }

    void IRemoteSettingsStateAware.OnRemoteSettingsChanged(bool isEnabled)
    {
        if (isEnabled)
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
            while (await _timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await SendReportAsync(cancellationToken).ConfigureAwait(false);

                await SendErrorCountReportAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            /* Do nothing */
        }
    }

    private async Task SendReportAsync(CancellationToken cancellationToken)
    {
        try
        {
            var telemetryEvent = GetPeriodicReportEvent();

            await _telemetryApiClient.SendEventAsync(telemetryEvent, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to send the periodic report: {Message}", ex.CombinedMessage());
        }
        finally
        {
            _statistics.Reset();
            _sharedWithMeItemCounters.Reset();
            _openedDocumentsCounters.Reset();
        }
    }

    private async Task SendErrorCountReportAsync(CancellationToken cancellationToken)
    {
        try
        {
            var telemetryEvents = TelemetryEvent.CreatePeriodicErrorCountEvent(_errorCountProvider.GetTopErrorCounts(maximumNumberOfCounters: 10));

            if (telemetryEvents.Events.Count == 0)
            {
                return; // Nothing to report
            }

            await _telemetryApiClient.SendEventsAsync(telemetryEvents, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to send the error count report: {Message}", ex.CombinedMessage());
        }
        finally
        {
            _errorCounter.Reset();
        }
    }

    private TelemetryEvent GetPeriodicReportEvent()
    {
        var values = new Dictionary<string, double>();
        var dimensions = new Dictionary<string, string>
            { { PeriodicReportConstants.PlanDimensionName, _userHasAPaidPlan is true ? PeriodicReportConstants.PaidPlan : PeriodicReportConstants.FreePlan } };

        values.Add(PeriodicReportMetricNames.NumberOfSyncPasses, _statistics.NumberOfSyncPasses);
        values.Add(PeriodicReportMetricNames.NumberOfUnhandledExceptionsDuringSync, _statistics.NumberOfUnhandledExceptionsDuringSync);
        values.Add(PeriodicReportMetricNames.NumberOfSuccessfulFileOperations, _statistics.NumberOfSuccessfulFileOperations);
        values.Add(PeriodicReportMetricNames.NumberOfSuccessfulFolderOperations, _statistics.NumberOfSuccessfulFolderOperations);
        values.Add(PeriodicReportMetricNames.NumberOfFailedFileOperations, _statistics.NumberOfFailedFileOperations);
        values.Add(PeriodicReportMetricNames.NumberOfFailedFolderOperations, _statistics.NumberOfFailedFolderOperations);

        var numberOfObjectNotFoundFailures = _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.ObjectNotFound);
        var numberOfDirectoryNotFoundFailures = _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.DirectoryNotFound);
        var numberOfPathNotFoundFailures = _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.PathNotFound);
        var numberOfItemNotFoundFailures = numberOfObjectNotFoundFailures + numberOfDirectoryNotFoundFailures + numberOfPathNotFoundFailures;

        values.Add(PeriodicReportMetricNames.NumberOfItemNotFoundFailures, numberOfItemNotFoundFailures);

        values.Add(
            PeriodicReportMetricNames.NumberOfUnauthorizedAccessFailures,
            _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.UnauthorizedAccess));

        values.Add(
            PeriodicReportMetricNames.NumberOfFreeSpaceExceededFailures,
            _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.FreeSpaceExceeded));

        values.Add(
            PeriodicReportMetricNames.NumberOfSharingViolationFailures,
            _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.SharingViolation));

        values.Add(
            PeriodicReportMetricNames.NumberOfTooManyChildrenFailures,
            _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.TooManyChildren));

        values.Add(
            PeriodicReportMetricNames.NumberOfDuplicateNameFailures,
            _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.DuplicateName));

        values.Add(
            PeriodicReportMetricNames.NumberOfInvalidNameFailures,
            _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.InvalidName));

        values.Add(
            PeriodicReportMetricNames.NumberOfPartialHydrationFailures,
            _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.Partial));

        values.Add(
            PeriodicReportMetricNames.NumberOfUnknownFailures,
            _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.Unknown));

        values.Add(
            PeriodicReportMetricNames.NumberOfIntegrityFailures,
            _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.IntegrityFailure));

        values.Add(
            PeriodicReportMetricNames.NumberOfFileUploadAbortedDueToFileChange,
            _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.TransferAbortedDueToFileChange));

        values.Add(
            PeriodicReportMetricNames.NumberOfSkippedFilesDueToLastWriteTimeTooRecent,
            _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.LastWriteTimeTooRecent));

        values.Add(
            PeriodicReportMetricNames.NumberOfMetadataMismatchFailures,
            _statistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.MetadataMismatch));

        var (numberOfSuccessfulItems, numberOfFailedItems) = _statistics.GetUniqueSyncedFileCounters();

        values.Add(PeriodicReportMetricNames.NumberOfSuccessfulItems, numberOfSuccessfulItems);
        values.Add(PeriodicReportMetricNames.NumberOfFailedItems, numberOfFailedItems);

        var (numberOfSuccessfullySyncedSharedWithMeItems, numberOfFailedToSyncSharedWithMeItems) = _statistics.GetUniqueSyncedSharedWithMeItemCounters();

        values.Add(PeriodicReportMetricNames.NumberOfSuccessfullySyncedSharedWithMeItems, numberOfSuccessfullySyncedSharedWithMeItems);
        values.Add(PeriodicReportMetricNames.NumberOfFailedToSyncSharedWithMeItems, numberOfFailedToSyncSharedWithMeItems);

        var (numberOfSuccessfulSharedWithMeItems, numberOfFailedSharedWithMeItems) = _sharedWithMeItemCounters.GetCounters();

        values.Add(PeriodicReportMetricNames.NumberOfSuccessfulSharedWithMeItems, numberOfSuccessfulSharedWithMeItems);
        values.Add(PeriodicReportMetricNames.NumberOfFailedSharedWithMeItems, numberOfFailedSharedWithMeItems);

        var (numberOfSuccessfullyOpenedDocuments, numberOfDocumentsThatCouldNotBeOpened) = _openedDocumentsCounters.GetCounters();

        values.Add(PeriodicReportMetricNames.NumberOfSuccessfullyOpenedDocuments, numberOfSuccessfullyOpenedDocuments);
        values.Add(PeriodicReportMetricNames.NumberOfDocumentsThatCouldNotBeOpened, numberOfDocumentsThatCouldNotBeOpened);

        AddDocumentNameMigrationStatistics(values);

        return TelemetryEvent.CreatePeriodicReportEvent(values.AsReadOnly(), dimensions.AsReadOnly());
    }

    private void AddDocumentNameMigrationStatistics(IDictionary<string, double> values)
    {
        values.Add(
            PeriodicReportMetricNames.NumberOfMigrationsStarted,
            _statistics.DocumentNameMigration.NumberOfMigrationsStarted);
        values.Add(
            PeriodicReportMetricNames.NumberOfMigrationsSkipped,
            _statistics.DocumentNameMigration.NumberOfMigrationsSkipped);
        values.Add(
            PeriodicReportMetricNames.NumberOfMigrationsCompleted,
            _statistics.DocumentNameMigration.NumberOfMigrationsCompleted);
        values.Add(
            PeriodicReportMetricNames.NumberOfMigrationsFailed,
            _statistics.DocumentNameMigration.NumberOfMigrationsFailed);
        values.Add(
            PeriodicReportMetricNames.NumberOfDocumentRenamingAttempts,
            _statistics.DocumentNameMigration.NumberOfDocumentRenamingAttempts);
        values.Add(
            PeriodicReportMetricNames.NumberOfFailedDocumentRenamingAttempts,
            _statistics.DocumentNameMigration.NumberOfFailedDocumentRenamingAttempts);
        values.Add(
            PeriodicReportMetricNames.NumberOfNonMappedDocuments,
            _statistics.DocumentNameMigration.NumberOfNonMappedDocuments);
        values.Add(
            PeriodicReportMetricNames.NumberOfRenamedDocuments,
            _statistics.DocumentNameMigration.NumberOfRenamedDocuments);
        values.Add(
            PeriodicReportMetricNames.NumberOfDocumentsNotRequiringRename,
            _statistics.DocumentNameMigration.NumberOfDocumentsNotRequiringRename);
    }
}
