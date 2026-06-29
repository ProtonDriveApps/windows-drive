using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local;

internal class LocalRootDirectory : IRootDirectory<long>
{
    public LocalRootDirectory(LocalReplica settings)
        : this(settings.Path, settings.RootFolderId)
    {
    }

    public LocalRootDirectory(string path, long id)
    {
        Path = path;
        Id = id;
    }

    public string Path { get; }
    public long Id { get; }
}
