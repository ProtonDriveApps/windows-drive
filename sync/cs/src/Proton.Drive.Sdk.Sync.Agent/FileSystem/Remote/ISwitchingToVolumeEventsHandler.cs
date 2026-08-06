using Proton.Drive.Sdk.Sync.Agent.Settings;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Remote;

internal interface ISwitchingToVolumeEventsHandler
{
    bool HasSwitched { get; }
    Task<bool> TrySwitchAsync(IReadOnlyCollection<RemoteToLocalMapping> mappings, CancellationToken cancellationToken);
}
