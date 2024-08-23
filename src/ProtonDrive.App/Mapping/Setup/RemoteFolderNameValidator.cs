using System;
using System.Collections.Generic;
using System.Linq;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping.Setup;

internal sealed class RemoteFolderNameValidator : IMappingsAware
{
    private IReadOnlyCollection<RemoteToLocalMapping> _activeMappings = Array.Empty<RemoteToLocalMapping>();

    public bool IsFolderNameInUse(string shareId, string name)
    {
        return _activeMappings.Any(m => m.Remote.ShareId == shareId
                                        && m.Remote.RootFolderName?.Equals(name, StringComparison.Ordinal) == true);
    }

    void IMappingsAware.OnMappingsChanged(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        _activeMappings = activeMappings;
    }
}
