using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Volumes;

public interface IVolumeClient
{
    public Task<IReadOnlyCollection<Volume>> GetVolumesAsync(CancellationToken cancellationToken);

    public Task<Volume> CreateVolumeAsync(CancellationToken cancellationToken);
}
