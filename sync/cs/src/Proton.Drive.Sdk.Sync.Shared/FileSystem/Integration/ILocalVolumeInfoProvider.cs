namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;

public interface ILocalVolumeInfoProvider
{
    public bool IsNtfsFileSystem(string path);

    /// <summary>
    /// Checks whether the file system of the volume containing the path is supported for classic
    /// (not on-demand) sync: NTFS, or ReFS when its file IDs can be used by the sync engine.
    /// </summary>
    public bool IsClassicSyncSupportedFileSystem(string path);

    long? GetAvailableFreeSpace(string path);

    public DriveType GetDriveType(string path);
}
