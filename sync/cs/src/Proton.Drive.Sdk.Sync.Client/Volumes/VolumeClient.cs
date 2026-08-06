using Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.Volumes;

internal class VolumeClient : IVolumeClient
{
    private readonly IVolumeApiClient _volumeApiClient;
    private readonly IVolumeCreationParametersFactory _volumeCreationParametersFactory;

    public VolumeClient(
        IVolumeApiClient volumeApiClient,
        IVolumeCreationParametersFactory volumeCreationParametersFactory)
    {
        _volumeApiClient = volumeApiClient;
        _volumeCreationParametersFactory = volumeCreationParametersFactory;
    }

    public async Task<IReadOnlyCollection<Volume>> GetVolumesAsync(CancellationToken cancellationToken)
    {
        var volumes = await _volumeApiClient.GetVolumesAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        return volumes.Volumes;
    }

    public async Task<Volume> CreateMainVolumeAsync(CancellationToken cancellationToken)
    {
        var parameters = await _volumeCreationParametersFactory.CreateForMainVolumeAsync(cancellationToken).ConfigureAwait(false);

        var response = await _volumeApiClient.CreateMainVolumeAsync(parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        return response.Volume;
    }

    public async Task<Volume> CreatePhotoVolumeAsync(CancellationToken cancellationToken)
    {
        var parameters = await _volumeCreationParametersFactory.CreateForPhotoVolumeAsync(cancellationToken).ConfigureAwait(false);

        var response = await _volumeApiClient.CreatePhotoVolumeAsync(parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        return response.Volume;
    }
}
