namespace ProtonDrive.App.Telemetry;

internal static class PeriodicReportMetricNames
{
    public const string NumberOfSyncPasses = "numberOfSyncPasses";
    public const string NumberOfUnhandledExceptionsDuringSync = "numberOfUnhandledExceptionsDuringSync";

    public const string NumberOfSuccessfulFileOperations = "numberOfSuccessfulFileOperations";
    public const string NumberOfSuccessfulFolderOperations = "numberOfSuccessfulFolderOperations";
    public const string NumberOfFailedFileOperations = "numberOfFailedFileOperations";
    public const string NumberOfFailedFolderOperations = "numberOfFailedFolderOperations";
    public const string NumberOfDuplicateNameFailures = "numberOfDuplicateNameFailures";
    public const string NumberOfInvalidNameFailures = "numberOfInvalidNameFailures";
    public const string NumberOfSharingViolationFailures = "numberOfSharingViolationFailures";
    public const string NumberOfUnauthorizedAccessFailures = "numberOfUnauthorizedAccessFailures";
    public const string NumberOfFreeSpaceExceededFailures = "numberOfFreeSpaceExceededFailures";
    public const string NumberOfTooManyChildrenFailures = "numberOfTooManyChildrenFailures";
    public const string NumberOfItemNotFoundFailures = "numberOfItemNotFoundFailures";
    public const string NumberOfPartialHydrationFailures = "numberOfPartialHydrationFailures";
    public const string NumberOfUnknownFailures = "numberOfUnknownFailures";
    public const string NumberOfIntegrityFailures = "numberOfIntegrityFailures";
    public const string NumberOfFileUploadAbortedDueToFileChange = "numberOfFileUploadAbortedDueToFileChange";
    public const string NumberOfSkippedFilesDueToLastWriteTimeTooRecent = "numberOfSkippedFilesDueToLastWriteTimeTooRecent";
    public const string NumberOfMetadataMismatchFailures = "numberOfMetadataMismatchFailures";

    public const string NumberOfSuccessfulItems = "numberOfSuccessfulItems";
    public const string NumberOfFailedItems = "numberOfFailedItems";

    public const string NumberOfSuccessfulSharedWithMeItems = "numberOfSuccessfulSharedWithMeItems";
    public const string NumberOfFailedSharedWithMeItems = "numberOfFailedSharedWithMeItems";
    public const string NumberOfSuccessfullySyncedSharedWithMeItems = "numberOfSuccessfullySyncedSharedWithMeItems";
    public const string NumberOfFailedToSyncSharedWithMeItems = "numberOfFailedToSyncSharedWithMeItems";

    public const string NumberOfSuccessfullyOpenedDocuments = "numberOfSuccessfullyOpenedDocuments";
    public const string NumberOfDocumentsThatCouldNotBeOpened = "numberOfDocumentsThatCouldNotBeOpened";

    public const string NumberOfMigrationsStarted = "numberOfDocumentRenamingMigrationsStarted";
    public const string NumberOfMigrationsSkipped = "numberOfDocumentRenamingMigrationsSkipped";
    public const string NumberOfMigrationsCompleted = "numberOfDocumentRenamingMigrationsCompleted";
    public const string NumberOfMigrationsFailed = "numberOfDocumentRenamingMigrationsFailed";
    public const string NumberOfDocumentRenamingAttempts = "numberOfDocumentRenamingAttempts";
    public const string NumberOfNonMappedDocuments = "numberOfNonMappedDocuments";
    public const string NumberOfFailedDocumentRenamingAttempts = "numberOfFailedDocumentRenamingAttempts";
    public const string NumberOfRenamedDocuments = "numberOfRenamedDocuments";
    public const string NumberOfDocumentsNotRequiringRename = "numberOfDocumentsNotRequiringRename";
}
