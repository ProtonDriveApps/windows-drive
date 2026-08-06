namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;

public interface ILocalFolderService
{
    bool FileExists(string path);
    bool FolderExists(string? path);
    bool NonEmptyFolderExists(string? path);
    bool EmptyFolderExists(string? path, ISet<string>? subfoldersToIgnore = null);
    bool TryGetFolderInfo(string path, FileShare shareMode, out LocalFolderInfo? folderInfo);
    bool TryDeleteEmptyFolder(string path);
    Task<bool> OpenFolderAsync(string? path);
    string? GetDefaultAccountRootFolderPath(string userDataPath, string? username);
    bool TryConvertToPlaceholder(string path);
    bool TrySetPinState(string path, FilePinState pinState);
    bool TryGetPinState(string path, out FilePinState pinState);
}
