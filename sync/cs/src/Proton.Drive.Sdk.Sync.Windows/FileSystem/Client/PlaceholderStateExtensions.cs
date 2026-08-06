using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Client;

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
