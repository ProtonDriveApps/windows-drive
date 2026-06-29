using System.Diagnostics.CodeAnalysis;
using System.Security;
using MoreLinq;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Media;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Client;

internal sealed class PhotoFileSystemClient : IPhotoFileSystemClient<long>
{
    private readonly IFileSystemClient<long> _fileSystemClient;

    public PhotoFileSystemClient(IFileSystemClient<long> fileSystemClient)
    {
        _fileSystemClient = fileSystemClient;
    }

    public void Connect(string syncRootPath, IFileHydrationDemandHandler<long> fileHydrationDemandHandler)
    {
        throw new NotSupportedException();
    }

    public Task DisconnectAsync()
    {
        throw new NotSupportedException();
    }

    public Task<NodeInfo<long>> GetInfoAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        return _fileSystemClient.GetInfoAsync(info, cancellationToken);
    }

    public IAsyncEnumerable<NodeInfo<long>> EnumerateAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Asynchronously performs a breadth-first traversal of the folder tree, enumerating non-system directories
    /// in alphabetical order, level by level.
    /// <para>
    /// All directories at the current depth are visited before descending into subdirectories.
    /// </para>
    /// </summary>
    /// <param name="info">The root folder node to start enumeration from.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns> An async stream representing non-system directories in breadth-first, alphabetical order. </returns>
    public IAsyncEnumerable<NodeInfo<long>> EnumerateFoldersAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var traverseBreadthFirstEnumerable = MoreEnumerable.TraverseBreadthFirst(
            info.Path,
            path => Directory.EnumerateDirectories(path)
                .Where(EntryIsNotSystemItem)
                .OrderBy(childPath => childPath, StringComparer.OrdinalIgnoreCase));

        return WithMappedException(traverseBreadthFirstEnumerable)
            .Select(path => NodeInfo<long>.Directory().WithPath(path).WithName(Path.GetFileName(path)))
            .ToAsyncEnumerable();
    }

    /// <summary>
    /// Asynchronously enumerates the files in the specified folder (non-recursive), returning only non-system files
    /// that have supported extensions, ordered alphabetically by file name.
    /// </summary>
    /// <param name="info">The folder node to enumerate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An async stream representing the filtered and ordered file paths.</returns>
    public IAsyncEnumerable<NodeInfo<long>> EnumeratePhotoFilesAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var photoFileEnumeration = Directory.EnumerateFiles(info.Path)
            .Where(EntryIsNotSystemItem)
            .Where(FileExtensionIsSupported)
            .OrderBy(childPath => childPath, StringComparer.OrdinalIgnoreCase);

        return WithMappedException(photoFileEnumeration)
            .Select(path => NodeInfo<long>.File().WithPath(path).WithName(Path.GetFileName(path)))
            .ToAsyncEnumerable();
    }

    /// <summary>
    /// Asynchronously enumerates all non-system files in a folder hierarchy using a breadth-first traversal strategy.
    /// <para>
    /// Directories are visited level by level in alphabetical order, and files within each directory
    /// are also returned in alphabetical order.
    /// </para>
    /// </summary>
    /// <param name="info">The root folder node to enumerate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns> An async stream representing non-system file paths, ordered alphabetically and traversed folder by folder (breadth-first).
    /// </returns>
    public IAsyncEnumerable<NodeInfo<long>> EnumerateAllPhotoFilesAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return EnumerateFoldersAsync(info, cancellationToken)
            .SelectMany(node => EnumeratePhotoFilesAsync(NodeInfo<long>.Directory().WithPath(node.Path), cancellationToken));
    }

    public Task<NodeInfo<long>> CreateDirectoryAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<IDestinationRevision<long>> CreateFileAsync(
        NodeInfo<long> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        return _fileSystemClient.OpenFileForReadingAsync(info, cancellationToken);
    }

    public Task<IDestinationRevision<long>> CreateRevisionAsync(
        NodeInfo<long> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task MoveAsync(IReadOnlyList<NodeInfo<long>> sourceNodes, NodeInfo<long> destinationInfo, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task MoveAsync(NodeInfo<long> info, NodeInfo<long> destinationInfo, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task DeleteAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task DeletePermanentlyAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task DeleteRevisionAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public void SetInSyncState(NodeInfo<long> info)
    {
        throw new NotSupportedException();
    }

    public Task HydrateFileAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    private static bool FileExtensionIsSupported(string path)
    {
        var extension = Path.GetExtension(path);

        return
            KnownFileExtensions.ImageExtensions.Contains(extension) ||
            KnownFileExtensions.VideoExtensions.Contains(extension);
    }

    private static bool EntryIsNotSystemItem(string path)
    {
        return (File.GetAttributes(path) & FileAttributes.System) == 0;
    }

    private static IEnumerable<T> WithMappedException<T>(IEnumerable<T> origin)
    {
        using var enumerator = WithMappedException(origin.GetEnumerator);

        while (true)
        {
            if (!WithMappedException(enumerator.MoveNext))
            {
                yield break;
            }

            // ReSharper disable once AccessToDisposedClosure
            yield return WithMappedException(() => enumerator.Current);
        }
    }

    private static T WithMappedException<T>(Func<T> origin)
    {
        try
        {
            return origin();
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    private static class ExceptionMapping
    {
        public static bool TryMapException(Exception exception, [MaybeNullWhen(false)] out Exception mappedException)
        {
            mappedException = exception switch
            {
                DirectoryNotFoundException => CreateFileSystemClientException(FileSystemErrorCode.DirectoryNotFound),
                IOException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),
                UnauthorizedAccessException or SecurityException => CreateFileSystemClientException(FileSystemErrorCode.UnauthorizedAccess),
                _ => null,
            };

            return mappedException is not null;

            FileSystemClientException CreateFileSystemClientException(FileSystemErrorCode errorCode)
            {
                return new FileSystemClientException(
                    exception.Message,
                    errorCode,
                    exception)
                {
                    IsInnerExceptionMessageAuthoritative = exception is IOException,
                };
            }
        }
    }
}
