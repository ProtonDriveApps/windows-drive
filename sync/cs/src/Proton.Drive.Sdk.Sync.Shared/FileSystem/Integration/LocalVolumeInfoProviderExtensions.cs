namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;

public static class LocalVolumeInfoProviderExtensions
{
    /// <summary>
    /// Determines whether <paramref name="directoryPath"/>'s volume has enough free space.
    /// When the free space cannot be determined, the check is skipped.
    /// </summary>
    public static bool HasInsufficientFreeSpace(this ILocalVolumeInfoProvider volumeInfoProvider, string? directoryPath, long requiredSpace)
    {
        if (requiredSpace <= 0 || string.IsNullOrEmpty(directoryPath))
        {
            return false;
        }

        var availableFreeSpace = volumeInfoProvider.GetAvailableFreeSpace(directoryPath);

        return availableFreeSpace is not null && availableFreeSpace < requiredSpace;
    }
}
