namespace Proton.Drive.Sdk.Sync.Agent.Settings.Remote;

public sealed record RemoteSettings
{
    public bool IsTelemetryEnabled { get; init; }
    public bool HasInAppNotificationsEnabled { get; init; }

    public static RemoteSettings Default { get; } = new();
}
