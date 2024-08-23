using System.Collections.Generic;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping;

public interface IMappingsAware
{
    void OnMappingsChanged(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        IReadOnlyCollection<RemoteToLocalMapping> deletedMappings);
}
