namespace ProtonDrive.Sync.Shared.FileSystem.Metadata.GoogleTakeout;

public sealed class GoogleTakeoutMetadata
{
    public DateTimeOffset? CaptureTime { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
}
