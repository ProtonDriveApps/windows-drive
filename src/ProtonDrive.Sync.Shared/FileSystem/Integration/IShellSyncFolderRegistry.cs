namespace ProtonDrive.Sync.Shared.FileSystem.Integration;

public interface IShellSyncFolderRegistry
{
    public void Register(string path);
    public void Unregister();
}
