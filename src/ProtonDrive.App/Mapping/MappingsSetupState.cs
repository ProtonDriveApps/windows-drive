using System;
using System.Collections.Generic;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping;

public record MappingsSetupState(MappingSetupStatus Status)
{
    public IReadOnlyCollection<RemoteToLocalMapping> Mappings { get; init; } = Array.Empty<RemoteToLocalMapping>();
    public MappingErrorCode ErrorCode { get; init; }

    public static MappingsSetupState None { get; } = new(MappingSetupStatus.None);
}
