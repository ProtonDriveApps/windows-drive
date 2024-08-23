using System;
using ProtonDrive.App.Settings;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Remote;

internal sealed class RemoteRootDirectory : IRootDirectory<string>
{
    public RemoteRootDirectory(RemoteToLocalMapping settings)
    {
        Id = settings.Remote.RootLinkType is LinkType.Folder
            ? settings.Remote.RootLinkId ?? throw new InvalidOperationException()
            : "virtual_" + settings.Id;
    }

    public string Id { get; }
    public string Path => string.Empty;
}
