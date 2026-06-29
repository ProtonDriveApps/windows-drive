using Proton.Drive.Sdk.Sync.Client.Shares.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;

internal sealed class PhotoVolumeCreationParameters
{
    public required ShareCreationParameters Share { get; init; }
    public required LinkCreationParameters Link { get; init; }
}
