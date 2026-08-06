using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

internal sealed class LocalSyncFolderValidator : ILocalSyncFolderValidator
{
    private readonly ILocalVolumeInfoProvider _localVolumeInfoProvider;
    private readonly INonSyncablePathProvider _nonSyncablePathProvider;
    private readonly ILocalFolderService _localFolderService;

    public LocalSyncFolderValidator(
        ILocalVolumeInfoProvider localVolumeInfoProvider,
        INonSyncablePathProvider nonSyncablePathProvider,
        ILocalFolderService localFolderService)
    {
        _localVolumeInfoProvider = localVolumeInfoProvider;
        _nonSyncablePathProvider = nonSyncablePathProvider;
        _localFolderService = localFolderService;
    }

    public SyncFolderValidationResult? ValidateDrive(string path)
    {
        return
            ValidateFolderExists(path) ??
            ValidateDriveType(path) ??
            ValidateFileSystem(path);
    }

    public SyncFolderValidationResult? ValidatePath(string path, IReadOnlySet<string> otherPaths)
    {
        path = PathComparison.EnsureTrailingSeparator(path);

        return
            ValidateFolderIsSyncable(path) ??
            ValidateFoldersDoNotOverlap(path, otherPaths);
    }

    public SyncFolderValidationResult? ValidateFolder(string path, bool shouldBeEmpty)
    {
        return
            ValidateFolderExists(path) ??
            ValidateFolderIsEmpty(path, shouldBeEmpty);
    }

    private static SyncFolderValidationResult? ValidateFolderExists(string path)
    {
        if (!Directory.Exists(path))
        {
            return SyncFolderValidationResult.LocalFolderDoesNotExist;
        }

        return null;
    }

    private static SyncFolderValidationResult? ValidateFoldersDoNotOverlap(string path, IReadOnlySet<string> otherPaths)
    {
        foreach (var otherPath in otherPaths.Select(PathComparison.EnsureTrailingSeparator))
        {
            if (path.StartsWith(otherPath, StringComparison.OrdinalIgnoreCase))
            {
                return SyncFolderValidationResult.FolderIncludedByAnAlreadySyncedFolder;
            }

            if (otherPath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
            {
                return SyncFolderValidationResult.FolderIncludesAnAlreadySyncedFolder;
            }
        }

        return null;
    }

    private SyncFolderValidationResult? ValidateDriveType(string path)
    {
        var driveType = _localVolumeInfoProvider.GetDriveType(path);

        return driveType switch
        {
            DriveType.Fixed => null,
            DriveType.Network => SyncFolderValidationResult.NetworkFolderNotSupported,
            DriveType.Unknown => SyncFolderValidationResult.LocalFileSystemAccessFailed,
            _ => SyncFolderValidationResult.LocalVolumeNotSupported,
        };
    }

    private SyncFolderValidationResult? ValidateFileSystem(string path)
    {
        if (!_localVolumeInfoProvider.IsNtfsFileSystem(path))
        {
            return SyncFolderValidationResult.LocalVolumeNotSupported;
        }

        return null;
    }

    private SyncFolderValidationResult? ValidateFolderIsSyncable(string path)
    {
        if (_nonSyncablePathProvider.Paths
            .Select(PathComparison.EnsureTrailingSeparator)
            .Any(prohibitedPath => path.StartsWith(prohibitedPath, StringComparison.OrdinalIgnoreCase) ||
                prohibitedPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)))
        {
            return SyncFolderValidationResult.NonSyncableFolder;
        }

        return null;
    }

    private SyncFolderValidationResult? ValidateFolderIsEmpty(string path, bool shouldBeEmpty)
    {
        try
        {
            if (shouldBeEmpty && _localFolderService.NonEmptyFolderExists(path))
            {
                return SyncFolderValidationResult.LocalFolderNotEmpty;
            }

            return null;
        }
        catch (Exception e) when (e.IsFileAccessException())
        {
            return SyncFolderValidationResult.LocalFileSystemAccessFailed;
        }
    }
}
