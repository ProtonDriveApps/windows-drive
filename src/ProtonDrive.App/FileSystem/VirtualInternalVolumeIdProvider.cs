namespace ProtonDrive.App.FileSystem;

internal static class VirtualInternalVolumeIdProvider
{
    public static int GetId(int volumeId, int mappingId)
    {
        return (1 << 31) + (volumeId << 16) + mappingId;
    }
}
