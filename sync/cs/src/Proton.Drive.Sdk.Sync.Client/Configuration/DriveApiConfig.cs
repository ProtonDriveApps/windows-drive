namespace Proton.Drive.Sdk.Sync.Client.Configuration;

public class DriveApiConfig
{
    public string? ClientVersion { get; set; }
    public string? UserAgent { get; set; }

    public string? SentryDsnPublicKey { get; internal set; }
    public string? SentryDsnProjectId { get; internal set; }

    public Uri? AuthBaseUrl { get; internal set; }
    public Uri? CoreBaseUrl { get; internal set; }
    public Uri? DataBaseUrl { get; internal set; }
    public Uri? DriveBaseUrl { get; internal set; }
    public Uri? DriveSdkBaseUrl { get; internal set; }
    public Uri? PaymentsBaseUrl { get; internal set; }
    public Uri? FeatureBaseUrl { get; internal set; }
    public Uri? ContactsBaseUrl { get; internal set; }

    public string? ContentType { get; set; }
    public TimeSpan Timeout { get; internal set; }
    public int DefaultNumberOfRetries { get; internal set; }
    public int DriveApiNumberOfRetries { get; internal set; }
    public int FileTransferNumberOfRetries { get; internal set; }
    public TimeSpan FileTransferDelayBetweenRetries { get; internal set; }
    public TimeSpan EventsPollingInterval { get; internal set; }
    public TimeSpan ForeignVolumeEventsPollingInterval { get; internal set; }
    public TimeSpan EventsThrottleInterval { get; internal set; }
    public TimeSpan DelayBeforeSwitchingOnline { get; internal set; }
    public TimeSpan FeaturesUpdateInterval { get; internal set; }
}
