using System;
using System.Collections.Generic;

namespace ProtonDrive.App.Settings;

internal sealed class MappingSettings
{
    private IReadOnlyCollection<RemoteToLocalMapping>? _mappings;

    public IReadOnlyCollection<RemoteToLocalMapping> Mappings
    {
        get => _mappings ??= Array.Empty<RemoteToLocalMapping>();
        init => _mappings = value;
    }

    public int LatestId { get; init; }
}
