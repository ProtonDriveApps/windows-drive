using System;
using System.Collections.Generic;

namespace ProtonDrive.Client.Devices.Contracts;

internal sealed record DeviceListResponse : ApiResponse
{
    private IReadOnlyCollection<DeviceListItem>? _devices;

    public IReadOnlyCollection<DeviceListItem> Devices
    {
        get => _devices ??= Array.Empty<DeviceListItem>();
        init => _devices = value;
    }
}
