namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Integration;

internal interface IOpenByFileIdSupportVerifier
{
    /// <summary>
    /// Verifies the file system can open the specified object by its file ID.
    /// Diagnostic only, never throws.
    /// </summary>
    bool SupportsOpenByFileId(FileSystemObject folder, int volumeSerialNumber);
}
