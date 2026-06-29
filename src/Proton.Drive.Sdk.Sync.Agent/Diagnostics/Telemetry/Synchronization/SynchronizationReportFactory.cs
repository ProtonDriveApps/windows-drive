using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.Synchronization;

internal static class SynchronizationReportFactory
{
    public static IEnumerable<TelemetryEvent> CreateReport(
        SyncStatistics syncStatistics,
        bool? userHasAPaidPlan,
        SharedWithMeItemCounters sharedWithMeItemCounters,
        OpenedDocumentsCounters openedDocumentsCounters)
    {
        var values = new Dictionary<string, double>();
        var dimensions = new Dictionary<string, string>
            { { PeriodicReportConstants.PlanDimensionName, userHasAPaidPlan is true ? PeriodicReportConstants.PaidPlan : PeriodicReportConstants.FreePlan } };

        values.Add(PeriodicReportMetricNames.NumberOfSyncPasses, syncStatistics.NumberOfSyncPasses);
        values.Add(PeriodicReportMetricNames.NumberOfUnhandledExceptionsDuringSync, syncStatistics.NumberOfUnhandledExceptionsDuringSync);
        values.Add(PeriodicReportMetricNames.NumberOfSuccessfulFileOperations, syncStatistics.NumberOfSuccessfulFileOperations);
        values.Add(PeriodicReportMetricNames.NumberOfSuccessfulFolderOperations, syncStatistics.NumberOfSuccessfulFolderOperations);
        values.Add(PeriodicReportMetricNames.NumberOfFailedFileOperations, syncStatistics.NumberOfFailedFileOperations);
        values.Add(PeriodicReportMetricNames.NumberOfFailedFolderOperations, syncStatistics.NumberOfFailedFolderOperations);

        var numberOfObjectNotFoundFailures = syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.ObjectNotFound);
        var numberOfDirectoryNotFoundFailures = syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.DirectoryNotFound);
        var numberOfPathNotFoundFailures = syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.PathNotFound);
        var numberOfItemNotFoundFailures = numberOfObjectNotFoundFailures + numberOfDirectoryNotFoundFailures + numberOfPathNotFoundFailures;

        values.Add(PeriodicReportMetricNames.NumberOfItemNotFoundFailures, numberOfItemNotFoundFailures);

        values.Add(
            PeriodicReportMetricNames.NumberOfUnauthorizedAccessFailures,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.UnauthorizedAccess));

        values.Add(
            PeriodicReportMetricNames.NumberOfFreeSpaceExceededFailures,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.FreeSpaceExceeded));

        values.Add(
            PeriodicReportMetricNames.NumberOfSharingViolationFailures,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.SharingViolation));

        values.Add(
            PeriodicReportMetricNames.NumberOfTooManyChildrenFailures,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.TooManyChildren));

        values.Add(
            PeriodicReportMetricNames.NumberOfDuplicateNameFailures,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.DuplicateName));

        values.Add(
            PeriodicReportMetricNames.NumberOfInvalidNameFailures,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.InvalidName));

        values.Add(
            PeriodicReportMetricNames.NumberOfPartialHydrationFailures,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.Partial));

        values.Add(
            PeriodicReportMetricNames.NumberOfUnknownFailures,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.Unknown));

        values.Add(
            PeriodicReportMetricNames.NumberOfCloudFileProviderNotRunningFailures,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.CloudFileProviderNotRunning));

        values.Add(
            PeriodicReportMetricNames.NumberOfCyclicRedundancyCheckFailures,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.CyclicRedundancyCheck));

        values.Add(
            PeriodicReportMetricNames.NumberOfIntegrityFailures,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.IntegrityFailure));

        values.Add(
            PeriodicReportMetricNames.NumberOfFileUploadAbortedDueToFileChange,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.TransferAbortedDueToFileChange));

        values.Add(
            PeriodicReportMetricNames.NumberOfSkippedFilesDueToLastWriteTimeTooRecent,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.LastWriteTimeTooRecent));

        values.Add(
            PeriodicReportMetricNames.NumberOfMetadataMismatchFailures,
            syncStatistics.GetNumberOfFailuresByErrorCode(FileSystemErrorCode.MetadataMismatch));

        var (numberOfSuccessfulItems, numberOfFailedItems) = syncStatistics.GetUniqueSyncedFileCounters();

        values.Add(PeriodicReportMetricNames.NumberOfSuccessfulItems, numberOfSuccessfulItems);
        values.Add(PeriodicReportMetricNames.NumberOfFailedItems, numberOfFailedItems);

        var (numberOfSuccessfullySyncedSharedWithMeItems, numberOfFailedToSyncSharedWithMeItems) = syncStatistics.GetUniqueSyncedSharedWithMeItemCounters();

        values.Add(PeriodicReportMetricNames.NumberOfSuccessfullySyncedSharedWithMeItems, numberOfSuccessfullySyncedSharedWithMeItems);
        values.Add(PeriodicReportMetricNames.NumberOfFailedToSyncSharedWithMeItems, numberOfFailedToSyncSharedWithMeItems);

        var (numberOfSuccessfulSharedWithMeItems, numberOfFailedSharedWithMeItems) = sharedWithMeItemCounters.GetCounters();

        values.Add(PeriodicReportMetricNames.NumberOfSuccessfulSharedWithMeItems, numberOfSuccessfulSharedWithMeItems);
        values.Add(PeriodicReportMetricNames.NumberOfFailedSharedWithMeItems, numberOfFailedSharedWithMeItems);

        var (numberOfSuccessfullyOpenedDocuments, numberOfDocumentsThatCouldNotBeOpened) = openedDocumentsCounters.GetCounters();

        values.Add(PeriodicReportMetricNames.NumberOfSuccessfullyOpenedDocuments, numberOfSuccessfullyOpenedDocuments);
        values.Add(PeriodicReportMetricNames.NumberOfDocumentsThatCouldNotBeOpened, numberOfDocumentsThatCouldNotBeOpened);

        return [CreatePeriodicReportEvent(values.AsReadOnly(), dimensions.AsReadOnly())];
    }

    private static TelemetryEvent CreatePeriodicReportEvent(IReadOnlyDictionary<string, double> values, IReadOnlyDictionary<string, string> dimensions)
    {
        const string measurementGroupName = "drive.windows.health";
        const string eventName = "periodic_report";

        return new TelemetryEvent(measurementGroupName, eventName, values, dimensions);
    }
}
