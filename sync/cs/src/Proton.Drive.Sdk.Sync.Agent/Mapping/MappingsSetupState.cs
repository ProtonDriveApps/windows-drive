using Proton.Drive.Sdk.Sync.Agent.Settings;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

public record MappingsSetupState(MappingSetupStatus Status)
{
    public IReadOnlyCollection<RemoteToLocalMapping> Mappings { get; init; } = [];
    public MappingErrorCode ErrorCode { get; init; }

    public static MappingsSetupState None { get; } = new(MappingSetupStatus.None);
}
