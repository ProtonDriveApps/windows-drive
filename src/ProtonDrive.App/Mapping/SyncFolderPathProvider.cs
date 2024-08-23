using System.Collections.Generic;
using System.IO;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared.Configuration;

namespace ProtonDrive.App.Mapping;

internal sealed class SyncFolderPathProvider : ISyncFolderPathProvider, IMappingsAware
{
    private readonly AppConfig _appConfig;
    private IReadOnlyCollection<RemoteToLocalMapping> _activeMappings = [];

    public SyncFolderPathProvider(AppConfig appConfig)
    {
        _appConfig = appConfig;
    }

    void IMappingsAware.OnMappingsChanged(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        _activeMappings = activeMappings;
    }

    public string? GetForeignDevicesFolderPath()
    {
        return _activeMappings.TryGetAccountRootFolderPath(out var accountRootFolderPath)
            ? Path.Combine(accountRootFolderPath, _appConfig.FolderNames.ForeignDevicesFolderName)
            : null;
    }

    public string? GetSharedWithMeItemsFolderPath()
    {
        return _activeMappings.TryGetAccountRootFolderPath(out var accountRootFolderPath)
            ? Path.Combine(accountRootFolderPath, _appConfig.FolderNames.SharedWithMeItemsFolderName)
            : null;
    }
}
