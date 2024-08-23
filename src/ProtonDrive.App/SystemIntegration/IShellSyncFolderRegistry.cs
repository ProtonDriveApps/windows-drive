namespace ProtonDrive.App.SystemIntegration;

public interface IShellSyncFolderRegistry
{
    public void Register(string path);
    public void Unregister();
}
