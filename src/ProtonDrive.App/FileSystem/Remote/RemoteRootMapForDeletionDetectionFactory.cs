using System;
using System.Collections.Generic;
using System.Linq;
using ProtonDrive.App.Devices;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.FileSystem.Remote;

internal sealed class RemoteRootMapForDeletionDetectionFactory : IDevicesAware
{
    private Device? _hostDevice;

    public (int VolumeId, IReadOnlyDictionary<string, IReadOnlyCollection<int>> NodeIdToRootMap) Create(
        IReadOnlyCollection<RemoteToLocalMapping> mappings)
    {
        var internalVolumeId = mappings.FirstOrDefault(m => m.Type is MappingType.HostDeviceFolder)?.Remote.InternalVolumeId ?? 0;

        var nodeIdToRootMap = mappings
            .Where(m => m.HasSetupSucceeded && m.Remote.InternalVolumeId == internalVolumeId && !string.IsNullOrEmpty(m.Remote.RootLinkId))
            .Select(m => (RootNodeId: m.Remote.RootLinkId ?? throw new InvalidOperationException(), RooId: m.Id))
            .GroupBy(x => x.RootNodeId, x => x.RooId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<int>)[.. g]);

        var hostDevice = _hostDevice;

        if (hostDevice != null && internalVolumeId != 0)
        {
            var rootIds = mappings
                .Where(m => m is { HasSetupSucceeded: true, Type: MappingType.HostDeviceFolder })
                .Select(m => m.Id)
                .ToList();

            // Monitor deletion of the whole host device
            nodeIdToRootMap.Add(hostDevice.LinkId, rootIds);
        }

        return (internalVolumeId, nodeIdToRootMap);
    }

    void IDevicesAware.OnDeviceChanged(DeviceChangeType changeType, Device device)
    {
        if (device.Type is not DeviceType.Host)
        {
            return;
        }

        _hostDevice = changeType switch
        {
            DeviceChangeType.Added => device,
            DeviceChangeType.Updated => device,
            DeviceChangeType.Removed => null,
            _ => throw new ArgumentOutOfRangeException(nameof(changeType), changeType, null),
        };
    }
}
