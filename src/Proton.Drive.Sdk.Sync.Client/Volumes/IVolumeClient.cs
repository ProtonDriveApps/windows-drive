using Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.Volumes;

public interface IVolumeClient
{
    public Task<IReadOnlyCollection<Volume>> GetVolumesAsync(CancellationToken cancellationToken);
    public Task<Volume> CreateMainVolumeAsync(CancellationToken cancellationToken);
    public Task<Volume> CreatePhotoVolumeAsync(CancellationToken cancellationToken);
}
