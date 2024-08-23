using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Volumes;

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

    public async Task<Volume> CreateVolumeAsync(CancellationToken cancellationToken)
    {
        var parameters = await _volumeCreationParametersFactory.CreateAsync(cancellationToken).ConfigureAwait(false);

        var response = await _volumeApiClient.CreateVolumeAsync(parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        return response.Volume;
    }
}
