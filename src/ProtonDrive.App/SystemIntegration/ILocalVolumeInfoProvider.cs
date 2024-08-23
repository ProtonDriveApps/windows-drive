using System.IO;

namespace ProtonDrive.App.SystemIntegration;

public interface ILocalVolumeInfoProvider
{
    public bool IsNtfsFileSystem(string path);

    long? GetAvailableFreeSpace(string path);

    public DriveType GetDriveType(string path);
}
