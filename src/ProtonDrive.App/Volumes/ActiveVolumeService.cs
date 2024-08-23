using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Volumes;

namespace ProtonDrive.App.Volumes;

internal sealed class ActiveVolumeService : IActiveVolumeService
{
    private readonly IVolumeClient _volumeClient;
    private readonly ILogger<ActiveVolumeService> _logger;

    public ActiveVolumeService(
        IVolumeClient volumeClient,
        ILogger<ActiveVolumeService> logger)
    {
        _volumeClient = volumeClient;
        _logger = logger;
    }

    public async Task<VolumeInfo> GetActiveVolumeAsync(CancellationToken cancellationToken)
    {
        var volumes = await _volumeClient.GetVolumesAsync(cancellationToken).ConfigureAwait(false);

        var volume = volumes.FirstOrDefault(v => v.State is Client.Contracts.VolumeState.Active)
                     ?? await CreateVolumeAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("The user has active volume with ID={VolumeId}", volume.Id);

        return ToVolumeInfo(volume);
    }

    private async Task<Volume> CreateVolumeAsync(CancellationToken cancellationToken)
    {
        var volume = await _volumeClient.CreateVolumeAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Created a new volume with ID={VolumeId}", volume.Id);

        return volume;
    }

    private VolumeInfo ToVolumeInfo(Volume volume)
    {
        return new VolumeInfo(volume.Id, volume.Share.Id, volume.Share.LinkId);
    }
}
