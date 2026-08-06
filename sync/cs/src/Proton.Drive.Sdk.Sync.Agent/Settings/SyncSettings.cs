namespace Proton.Drive.Sdk.Sync.Agent.Settings;

public sealed class SyncSettings
{
    public bool Paused { get; set; }

    public DateTime? PausedUntil { get; set; }
}
