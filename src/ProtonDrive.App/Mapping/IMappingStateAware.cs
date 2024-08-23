using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping;

public interface IMappingStateAware
{
    void OnMappingStateChanged(RemoteToLocalMapping mapping, MappingState state);
}
