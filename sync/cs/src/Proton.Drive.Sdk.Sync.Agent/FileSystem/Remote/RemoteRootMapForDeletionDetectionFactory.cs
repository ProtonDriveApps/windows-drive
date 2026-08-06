using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Remote;

internal sealed class RemoteRootMapForDeletionDetectionFactory
{
    public (int VolumeId, IReadOnlyDictionary<string, IReadOnlyCollection<int>> NodeIdToRootMap) Create(
        IReadOnlyCollection<RemoteToLocalMapping> mappings)
    {
        var internalVolumeId = mappings.FirstOrDefault(m => m.Type is MappingType.HostDeviceFolder)?.Remote.InternalVolumeId ?? 0;

        var nodeIdToRootMap = mappings
            .Where(m => m.HasSetupSucceeded && m.Remote.InternalVolumeId == internalVolumeId && !string.IsNullOrEmpty(m.Remote.RootLinkId))
            .Select(m => (RootNodeId: m.Remote.RootLinkId ?? throw new InvalidOperationException(), RooId: m.Id))
            .GroupBy(x => x.RootNodeId, x => x.RooId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<int>)[.. g]);

        return (internalVolumeId, nodeIdToRootMap);
    }
}
