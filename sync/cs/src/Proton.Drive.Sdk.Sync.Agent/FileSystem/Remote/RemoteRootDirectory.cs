using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Remote;

internal sealed class RemoteRootDirectory : IRootDirectory<string>
{
    public RemoteRootDirectory(RemoteToLocalMapping settings)
    {
        Id = settings.Remote.RootItemType is LinkType.Folder
            ? settings.Remote.RootLinkId ?? throw new InvalidOperationException()
            : RootPropertyProvider.GetVirtualRootFolderId(settings.Id);
    }

    public string Id { get; }
    public string Path => string.Empty;
}
