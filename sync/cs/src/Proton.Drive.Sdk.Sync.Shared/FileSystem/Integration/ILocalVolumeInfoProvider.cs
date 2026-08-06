namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;

public interface ILocalVolumeInfoProvider
{
    public bool IsNtfsFileSystem(string path);

    long? GetAvailableFreeSpace(string path);

    public DriveType GetDriveType(string path);
}
