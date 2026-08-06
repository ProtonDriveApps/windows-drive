using Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.Volumes;

internal interface IVolumeCreationParametersFactory
{
    Task<VolumeCreationParameters> CreateForMainVolumeAsync(CancellationToken cancellationToken);
    Task<PhotoVolumeCreationParameters> CreateForPhotoVolumeAsync(CancellationToken cancellationToken);
}
