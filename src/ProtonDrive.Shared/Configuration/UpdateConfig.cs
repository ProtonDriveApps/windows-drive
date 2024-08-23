using System;

namespace ProtonDrive.Shared.Configuration;

public sealed class UpdateConfig
{
    public string UpdateUrl { get; internal set; } = string.Empty;
    public string DownloadFolderPath { get; internal set; } = string.Empty;
    public TimeSpan MinProgressDuration { get; internal set; }
    public TimeSpan CheckInterval { get; internal set; }
    public TimeSpan NotificationInterval { get; internal set; }
    public TimeSpan CleanupDelay { get; internal set; }
    public TimeSpan Timeout { get; internal set; }
    public int NumberOfRetries { get; internal set; }
}
