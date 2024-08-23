using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Client.Devices;

public interface IDeviceClient
{
    Task<IReadOnlyCollection<Device>> GetAllAsync(CancellationToken cancellationToken);
    Task<Device> CreateAsync(string volumeId, string name, CancellationToken cancellationToken);
    Task<Device> RenameAsync(Device device, string name, CancellationToken cancellationToken);
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
