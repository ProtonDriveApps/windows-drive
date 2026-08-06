namespace Proton.Drive.Shared.Configuration;

public sealed class AppConfig
{
#if DEBUG
    public const bool DefaultTlsPinningEnabled = false;
#else
    public const bool DefaultTlsPinningEnabled = true;
#endif

    public string AppName { get; internal set; } = string.Empty;
    public Version AppVersion { get; internal set; } = new();
    public string ApplicationId => "Proton.ProtonDrive";
    public string AppFolderPath { get; internal set; } = string.Empty;
    public string AppLaunchPath { get; internal set; } = string.Empty;
    public string AppDataPath { get; internal set; } = string.Empty;
    public string UserDataPath { get; internal set; } = string.Empty;
    public string WebView2DataPath { get; internal set; } = string.Empty;

    public TimeSpan UserUpdateInterval { get; internal set; }
    public TimeSpan OffersUpdateInterval { get; internal set; }
    public TimeSpan MinFailedSetupRetryInterval { get; internal set; }
    public TimeSpan MaxFailedSetupRetryInterval { get; internal set; }
    public TimeSpan FailedDataRetrievalRetryInterval { get; internal set; }
    public TimeSpan FileSyncStateMaintenanceInterval { get; internal set; }
    public TimeSpan MaxLocalFileAccessRetryInterval { get; internal set; }
    public TimeSpan MaxRemoteFileAccessRetryInterval { get; internal set; }
    public TimeSpan MaxFileRevisionCreationInterval { get; internal set; }
    public TimeSpan MinDelayBeforeFileUpload { get; set; }
    public TimeSpan ContactsCacheInvalidationInterval { get; set; }
    public TimeSpan ContactsCacheGraceInterval { get; set; }
    public TimeSpan DelayBeforeDisplayingSyncInitializationProgress { get; internal set; }
    public TimeSpan DelayBeforeShowingNotification { get; internal set; }

    public TimeSpan PeriodicTelemetryReportInterval { get; internal set; }
    public TimeSpan PeriodicObservabilityReportInterval { get; internal set; }
    public TimeSpan PeriodicFailuresImpactedUsersReportInterval { get; internal set; }
    public TimeSpan PeriodicTransferPerformanceReportInterval { get; internal set; }
    public TimeSpan PeriodicThumbnailGenerationReportInterval { get; internal set; }
    public TimeSpan MaxInactivityPeriodBetweenFileTransfers { get; internal set; }

    public TimeSpan ActivityQueryInterval { get; internal set; }
    public int NumberOfDaysBeforeRemovingInstallationLogFiles { get; internal set; }

    public bool TlsPinningEnabled { get; internal set; } = DefaultTlsPinningEnabled;

    /// <summary>
    /// Ignores remote SSL certificate errors.
    /// Only has effect when the <see cref="TlsPinningEnabled"/> is false.
    /// </summary>
    public bool RemoteCertificateErrorsIgnored { get; internal set; }

    public FolderNameConfig FolderNames { get; } = new();

    public int MaxNumberOfConcurrentFileTransfers { get; internal set; }
    public int MaxNumberOfSyncedSharedWithMeItems { get; internal set; }
    public TimeSpan FileConsistencyGuardRetryInterval { get; internal set; }
    public DateTimeOffset FileConsistencyGuardNotApplicableSince { get; internal set; }
}
