namespace Proton.Drive.Sdk.Sync.Agent.Settings;

public sealed class SyncSettings
{
    public bool Paused { get; set; }

    public DateTimeOffset? PausedUntil { get; set; }
}
