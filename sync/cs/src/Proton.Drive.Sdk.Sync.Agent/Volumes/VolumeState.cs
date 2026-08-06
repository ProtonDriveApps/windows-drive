namespace Proton.Drive.Sdk.Sync.Agent.Volumes;

public sealed record VolumeState(VolumeStatus Status, VolumeInfo? Volume, string? ErrorMessage = null)
{
    public static VolumeState Idle { get; } = new(VolumeStatus.Idle, Volume: null);
}
