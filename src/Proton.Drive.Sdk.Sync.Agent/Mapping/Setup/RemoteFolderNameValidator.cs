using Proton.Drive.Sdk.Sync.Agent.Settings;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup;

internal sealed class RemoteFolderNameValidator : IMappingsAware
{
    private IReadOnlyCollection<RemoteToLocalMapping> _activeMappings = [];

    public bool IsFolderNameInUse(string shareId, string name)
    {
        return _activeMappings.Any(
            m => m.Remote.ShareId == shareId
                && m.Remote.RootItemName?.Equals(name, StringComparison.Ordinal) == true);
    }

    void IMappingsAware.OnMappingsChanged(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        _activeMappings = activeMappings;
    }
}
