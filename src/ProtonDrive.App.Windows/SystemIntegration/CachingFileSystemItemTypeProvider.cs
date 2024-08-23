using System;
using System.Collections.Concurrent;
using System.IO;

namespace ProtonDrive.App.Windows.SystemIntegration;

/// <summary>
/// A caching decorator for the <see cref="IFileSystemItemTypeProvider"/>.
/// </summary>
/// <remarks>
/// There is no cache invalidation. If the user changes the default program for a file type,
/// the application will not reflect that until it is restarted.
/// </remarks>
internal sealed class CachingFileSystemItemTypeProvider : IFileSystemItemTypeProvider
{
    private readonly IFileSystemItemTypeProvider _origin;

    private readonly ConcurrentDictionary<string, string?> _fileTypeCache = new();
    private readonly Lazy<string?> _folderType;

    public CachingFileSystemItemTypeProvider(IFileSystemItemTypeProvider origin)
    {
        _origin = origin;

        _folderType = new Lazy<string?>(() => _origin.GetFolderType());
    }

    public string? GetFileType(string filename)
    {
        return _fileTypeCache.GetOrAdd(Path.GetExtension(filename), _ => _origin.GetFileType(filename));
    }

    public string? GetFolderType()
    {
        return _folderType.Value;
    }
}
