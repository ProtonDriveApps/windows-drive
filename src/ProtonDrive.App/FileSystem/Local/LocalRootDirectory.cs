using ProtonDrive.App.Settings;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Local;

internal class LocalRootDirectory : IRootDirectory<long>
{
    public LocalRootDirectory(LocalReplica settings)
        : this(settings.RootFolderPath, settings.RootFolderId)
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
