using System;
using System.Collections.Generic;
using System.Linq;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping.Setup;

internal sealed class VolumeIdentityProvider : IMappingsAware
{
    private IReadOnlyCollection<RemoteToLocalMapping> _activeMappings = Array.Empty<RemoteToLocalMapping>();

    public int GetLocalVolumeId(int volumeSerialNumber)
    {
        var existingVolumeId = _activeMappings
            .Where(m => m.Local.VolumeSerialNumber == volumeSerialNumber)
            .Select(m => m.Local.InternalVolumeId)
            .FirstOrDefault(x => x != default);

        if (existingVolumeId != default)
        {
            return existingVolumeId;
        }

        var maxVolumeId = _activeMappings.DefaultIfEmpty().Max(m => m?.Local.InternalVolumeId ?? 0);

        return maxVolumeId + 1;
    }

    public int GetRemoteVolumeId(string volumeId)
    {
        var existingVolumeId = _activeMappings
            .Where(m => m.Remote.VolumeId == volumeId)
            .Select(m => m.Remote.InternalVolumeId)
            .FirstOrDefault(x => x != default);

        if (existingVolumeId != default)
        {
            return existingVolumeId;
        }

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
