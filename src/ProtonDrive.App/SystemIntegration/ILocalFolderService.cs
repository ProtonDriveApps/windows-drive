using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ProtonDrive.App.SystemIntegration;

public interface ILocalFolderService
{
    bool FileExists(string path);
    bool FolderExists(string? path);
    bool NonEmptyFolderExists(string? path);
    bool EmptyFolderExists(string? path, ISet<string>? subfoldersToIgnore = null);
    bool TryGetFolderInfo(string path, FileShare shareMode, out LocalFolderInfo? folderInfo);
    Task<bool> OpenFolderAsync(string? path);
    string? GetDefaultAccountRootFolderPath(string userDataPath, string? username);
    public bool TryConvertToPlaceholder(string path);
}
