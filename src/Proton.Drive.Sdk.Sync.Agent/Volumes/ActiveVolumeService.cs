using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Client.Volumes;
using Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;

namespace Proton.Drive.Sdk.Sync.Agent.Volumes;

internal sealed class ActiveVolumeService : IActiveVolumeService
{
    private readonly IVolumeClient _volumeClient;
    private readonly ILogger<ActiveVolumeService> _logger;

    private IReadOnlyCollection<Volume>? _cachedVolumes;

    public ActiveVolumeService(
        IVolumeClient volumeClient,
        ILogger<ActiveVolumeService> logger)
    {
        _volumeClient = volumeClient;
        _logger = logger;
    }

    public async Task<VolumeInfo?> GetMainVolumeAsync(CancellationToken cancellationToken)
    {
        var volumes = await _volumeClient.GetVolumesAsync(cancellationToken).ConfigureAwait(false);

        // The list of volumes is cached to be used for obtaining a Photo volume
        Interlocked.Exchange(ref _cachedVolumes, volumes);

        return GetActiveVolume(volumes, VolumeType.Main);
    }

    public async Task<VolumeInfo> CreateMainVolumeAsync(CancellationToken cancellationToken)
    {
        var volume = await _volumeClient.CreateMainVolumeAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Created {Type} volume with ID \"{VolumeId}\"", volume.Type, volume.Id);

        return GetVolumeInfo(volume);
    }

    public async Task<VolumeInfo?> GetPhotoVolumeAsync(CancellationToken cancellationToken)
    {
        return
            TryGetPhotoVolumeFromCache(out var volume)
                ? volume
                : await GetPhotoVolumeFromRemoteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<VolumeInfo> CreatePhotoVolumeAsync(CancellationToken cancellationToken)
    {
        var volume = await _volumeClient.CreatePhotoVolumeAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Created {Type} volume with ID \"{VolumeId}\"", volume.Type, volume.Id);

        return GetVolumeInfo(volume);
    }

    private static VolumeInfo GetVolumeInfo(Volume volume)
    {
        return new VolumeInfo(volume.Id, volume.Share.Id, volume.Share.LinkId);
    }

    private bool TryGetPhotoVolumeFromCache(out VolumeInfo? volume)
    {
        // The cached volumes are cleared upon attempting to obtain a Photo volume,
        // so that further attempts do not hit the outdated cache.
        var volumes = Interlocked.Exchange(ref _cachedVolumes, null);

        if (volumes is null)
        {
            volume = null;
            return false;
        }

        volume = GetActiveVolume(volumes, VolumeType.Photo);
        return true;
    }

    private async Task<VolumeInfo?> GetPhotoVolumeFromRemoteAsync(CancellationToken cancellationToken)
    {
        var volumes = await _volumeClient.GetVolumesAsync(cancellationToken).ConfigureAwait(false);

        return GetActiveVolume(volumes, VolumeType.Photo);
    }

    private VolumeInfo? GetActiveVolume(IReadOnlyCollection<Volume>? volumes, VolumeType type)
    {
        var volume = volumes?.FirstOrDefault(v => v.State is Proton.Drive.Sdk.Sync.Client.Volumes.Contracts.VolumeState.Active && v.Type == type);

        if (volume is null)
        {
            return null;
        }

        _logger.LogInformation("The user has active {Type} volume with ID \"{VolumeId}\", root share with ID \"{ShareId}\"", volume.Type, volume.Id, volume.Share.Id);

        return GetVolumeInfo(volume);
    }
}
