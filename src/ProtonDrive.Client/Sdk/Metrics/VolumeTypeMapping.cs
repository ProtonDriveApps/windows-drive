using Proton.Drive.Sdk.Telemetry;

namespace ProtonDrive.Client.Sdk.Metrics;

internal static class VolumeTypeMapping
{
    private static readonly Dictionary<VolumeType, string> Mapping = new()
    {
        { VolumeType.OwnVolume, "own_volume" },
        { VolumeType.OwnPhotosVolume, "own_photo_volume" },
        { VolumeType.Shared, "shared" },
        { VolumeType.SharedPublic, "shared_public" },
    };

    public static string GetValueOrDefault(VolumeType volumeType) => Mapping.GetValueOrDefault(volumeType, "unknown");
}
