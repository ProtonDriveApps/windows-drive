using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup;

internal sealed class VolumeIdentityProvider : IMappingsAware
{
    private IReadOnlyCollection<RemoteToLocalMapping> _activeMappings = [];

    public int GetLocalVolumeId(int volumeSerialNumber)
    {
        // Shared with me file mappings have unique local internal volume IDs, making local file roots belong to different virtual volumes
        var existingVolumeId = _activeMappings
            .Where(m => m.Local.VolumeSerialNumber == volumeSerialNumber && (m.Type is not MappingType.SharedWithMeItem || m.Remote.RootItemType is not LinkType.File))
            .Select(m => m.Local.InternalVolumeId)
            .FirstOrDefault(x => x != 0);

        if (existingVolumeId != 0)
        {
            return existingVolumeId;
        }

        return GetUniqueLocalVolumeId();
    }

    public int GetUniqueLocalVolumeId()
    {
        var maxVolumeId = _activeMappings.DefaultIfEmpty().Max(m => m?.Local.InternalVolumeId ?? 0);

        return maxVolumeId + 1;
    }

    public int GetRemoteVolumeId(string volumeId)
    {
        // Shared with me item mappings have unique remote internal volume IDs, making all remote roots belong to different virtual volumes
        var existingVolumeId = _activeMappings
            .Where(m => m.Remote.VolumeId == volumeId && m.Type is not MappingType.SharedWithMeItem)
            .Select(m => m.Remote.InternalVolumeId)
            .FirstOrDefault(x => x != 0);

        if (existingVolumeId != 0)
        {
            return existingVolumeId;
        }

        return GetUniqueRemoteVolumeId();
    }

    public int GetUniqueRemoteVolumeId()
    {
        var maxVolumeId = _activeMappings.DefaultIfEmpty().Max(m => m?.Remote.InternalVolumeId ?? 0);

        return maxVolumeId + 1;
    }

    void IMappingsAware.OnMappingsChanged(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        _activeMappings = activeMappings;
    }
}
