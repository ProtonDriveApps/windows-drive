using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local;

internal sealed class LocalRootMapForDeletionDetectionFactory
{
    public (int VolumeId, IReadOnlyDictionary<long, IReadOnlyCollection<int>> NodeIdToRootMap) Create(
        IReadOnlyCollection<RemoteToLocalMapping> mappings)
    {
        var internalVolumeId = mappings.FirstOrDefault(m => m is { HasSetupSucceeded: true, Type: MappingType.CloudFiles })?.Local.InternalVolumeId ?? 0;

        var nodeIdToRootMap = mappings
            .Where(m => m.HasSetupSucceeded && m.Local.InternalVolumeId == internalVolumeId)
            .Select(m => (RootNodeId: m.Local.RootFolderId, RooId: m.Id))
            .GroupBy(x => x.RootNodeId, x => x.RooId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<int>)[.. g]);

        return (internalVolumeId, nodeIdToRootMap);
    }
}
