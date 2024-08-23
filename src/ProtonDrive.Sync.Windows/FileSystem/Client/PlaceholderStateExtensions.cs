using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal static class PlaceholderStateExtensions
{
    public static PlaceholderState ThrowIfInvalid(this PlaceholderState value)
    {
        if (value.HasFlag(PlaceholderState.Invalid))
        {
            throw new FileSystemClientException<long>("Failed to parse placeholder state");
        }

        return value;
    }
}
