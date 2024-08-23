using System;

namespace ProtonDrive.Update.Config;

/// <summary>
/// Configuration data for Update module. It should be registered
/// in a Service Container.
/// </summary>
public class AppUpdateConfig
{
    public AppUpdateConfig(
        string checkForUpdateHttpClientName,
        string downloadUpdateHttpClientName,
        Uri feedUri,
        double rolloutEligibilityThreshold,
        Version currentVersion,
        string updatesFolderPath,
        string earlyAccessCategoryName,
        TimeSpan minProgressDuration,
        TimeSpan cleanupDelay)
    {
        CheckForUpdateHttpClientName = checkForUpdateHttpClientName;
        DownloadUpdateHttpClientName = downloadUpdateHttpClientName;
        FeedUri = feedUri;
        RolloutEligibilityThreshold = rolloutEligibilityThreshold;
        CurrentVersion = currentVersion;
        UpdatesFolderPath = updatesFolderPath;
        EarlyAccessCategoryName = earlyAccessCategoryName;
        MinProgressDuration = minProgressDuration;
        CleanupDelay = cleanupDelay;
    }

    public string CheckForUpdateHttpClientName { get; }
    public string DownloadUpdateHttpClientName { get; }
    public Uri FeedUri { get; }
    public double RolloutEligibilityThreshold { get; }
    public Version CurrentVersion { get; }
    public string UpdatesFolderPath { get; }
    public string EarlyAccessCategoryName { get; }
    public TimeSpan MinProgressDuration { get; }
    public TimeSpan CleanupDelay { get; }
}
