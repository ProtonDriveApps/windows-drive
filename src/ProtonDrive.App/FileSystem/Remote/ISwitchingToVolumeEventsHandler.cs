using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.FileSystem.Remote;

internal interface ISwitchingToVolumeEventsHandler
{
    bool HasSwitched { get; }
    Task<bool> TrySwitchAsync(IReadOnlyCollection<RemoteToLocalMapping> mappings, CancellationToken cancellationToken);
}
