namespace ProtonDrive.App.Volumes;

public sealed record VolumeState(VolumeServiceStatus Status, VolumeInfo? Volume, string? ErrorMessage = default)
{
    public static VolumeState Idle { get; } = new(VolumeServiceStatus.Idle, default);
}
