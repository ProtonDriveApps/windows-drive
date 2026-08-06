using Proton.Drive.Sdk.Sync.Agent.Settings;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

public interface IMappingStateAware
{
    void OnMappingStateChanged(RemoteToLocalMapping mapping, MappingState state);
}
