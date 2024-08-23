using System;

namespace ProtonDrive.Client.Configuration;

public class DriveApiConfig
{
    public Uri? AuthBaseUrl { get; internal set; }
    public Uri? CoreBaseUrl { get; internal set; }
    public Uri? DataBaseUrl { get; internal set; }
    public Uri? DriveBaseUrl { get; internal set; }
    public Uri? PaymentsBaseUrl { get; internal set; }
    public Uri? FeatureBaseUrl { get; internal set; }
    public Uri? DocsBaseUrl { get; internal set; }
    public string? ClientVersion { get; set; }
    public string? UserAgent { get; set; }
    public string? ContentType { get; set; }
    public TimeSpan Timeout { get; internal set; }
    public TimeSpan BlocksTimeout { get; internal set; }
    public TimeSpan RevisionUpdateTimeout { get; internal set; }
    public int DefaultNumberOfRetries { get; internal set; }
    public int DriveApiNumberOfRetries { get; internal set; }
    public TimeSpan EventsPollingInterval { get; internal set; }
    public TimeSpan ForeignVolumeEventsPollingInterval { get; internal set; }
    public int ConsecutiveErrorsBeforeSwitchingOffline { get; internal set; }
    public TimeSpan DelayBeforeSwitchingOnline { get; internal set; }
    public TimeSpan FeaturesUpdateInterval { get; internal set; }
}
