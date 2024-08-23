using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.IO;

namespace ProtonDrive.Sync.Shared.FileSystem;

/// <summary>
/// A File System Client interface common for both local and remote cloud file systems.
/// </summary>
/// <typeparam name="TId">The type of the file system object identity.</typeparam>
public interface IFileSystemClient<TId>
    where TId : IEquatable<TId>
{
    void Connect(string syncRootPath, IFileHydrationDemandHandler<TId> fileHydrationDemandHandler);

    Task DisconnectAsync();

    /// <summary>
    /// Retrieves information about the specified file system object.
    /// </summary>
    /// <param name="info">The file system information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The fresh file system node information.</returns>
    /// <remarks>
    /// The expected content of the <paramref name="info"/> argument:
    /// <list type="bullet">
    /// <item><term>Path</term><description>The file system object path.</description></item>
    /// <item><term>Id</term><description>The file system object identity.</description></item>
    /// <item><term>ParentId</term><description>The parent directory identity.</description></item>
    /// <item><term>Name</term><description>The file system object name.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="FileSystemClientException{TId}">File system client specific exception has occurred.
    /// Expected specific <see cref="FileSystemClientException.ErrorCode"/> values:
    /// <see cref="FileSystemErrorCode.ObjectNotFound">The file system object does not exist.</see>
    /// <see cref="FileSystemErrorCode.PathNotFound">The parent directory cannot be found at the specified path.</see>
    /// <see cref="FileSystemErrorCode.DirectoryNotFound">The part of the parent directory path does not exist.</see>
    /// <see cref="FileSystemErrorCode.IdentityMismatch">The parent directory identity does not match the expected.</see>
    /// <see cref="FileSystemErrorCode.MetadataMismatch">The parent file system object is a file.</see>
    /// <see cref="FileSystemErrorCode.SharingViolation">The parent directory is already open in not compatible sharing mode.</see>
    /// <see cref="FileSystemErrorCode.UnauthorizedAccess">Access has been denied.</see>
    /// </exception>
    Task<NodeInfo<TId>> GetInfo(NodeInfo<TId> info, CancellationToken cancellationToken);

    /// <summary>
    /// Returns an enumerable collection of file system object information in the specified directory.
    /// </summary>
    /// <param name="info">The directory information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>An asynchronously enumerable collection of file system node information.</returns>
    /// <remarks>
    /// The expected content of the <paramref name="info"/> argument:
    /// <list type="bullet">
    /// <item><term>Path</term><description>The directory path.</description></item>
    /// <item><term>Id</term><description>The directory identity.</description></item>
    /// <item><term>Name</term><description>The directory name.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="FileSystemClientException{TId}">File system client specific exception has occurred.
    /// Expected specific <see cref="FileSystemClientException.ErrorCode"/> values:
    /// <see cref="FileSystemErrorCode.ObjectNotFound">The directory does not exist.</see>
    /// <see cref="FileSystemErrorCode.PathNotFound">The directory cannot be found at the specified path.</see>
    /// <see cref="FileSystemErrorCode.DirectoryNotFound">The part of the directory path does not exist.</see>
    /// <see cref="FileSystemErrorCode.IdentityMismatch">The directory identity does not match the expected.</see>
    /// <see cref="FileSystemErrorCode.MetadataMismatch">The file system object is a file.</see>
    /// <see cref="FileSystemErrorCode.SharingViolation">The directory is already open in not compatible sharing mode.</see>
    /// <see cref="FileSystemErrorCode.UnauthorizedAccess">Access has been denied.</see>
    /// </exception>
    IAsyncEnumerable<NodeInfo<TId>> Enumerate(NodeInfo<TId> info, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new directory.
    /// </summary>
    /// <param name="info">The new directory information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The file system information object with the created directory information.</returns>
    /// <remarks>
    /// The expected content of the <paramref name="info"/> argument:
    /// <list type="bullet">
    /// <item><term>Path</term><description>The path of the directory to create.</description></item>
    /// <item><term>ParentId</term><description>The parent directory identity.</description></item>
    /// <item><term>Name</term><description>The name of the directory to create.</description></item>
    /// </list>
    /// </remarks>
    /// Expected specific <see cref="FileSystemClientException.ErrorCode"/> values:
    /// <exception cref="FileSystemClientException{TId}">File system client specific exception has occurred.
    /// <see cref="FileSystemErrorCode.ObjectNotFound">The parent directory does not exist.</see>
    /// <see cref="FileSystemErrorCode.PathNotFound">The parent directory cannot be found at the specified path or the just created directory cannot be found to retrieve its information.</see>
    /// <see cref="FileSystemErrorCode.DirectoryNotFound">The part of the parent directory path does not exist.</see>
    /// <see cref="FileSystemErrorCode.IdentityMismatch">The parent directory identity does not match the expected.</see>
    /// <see cref="FileSystemErrorCode.MetadataMismatch">The parent file system object is a file.</see>
    /// <see cref="FileSystemErrorCode.DuplicateName">The file system object with the specified name already exists on the parent directory.</see>
    /// <see cref="FileSystemErrorCode.SharingViolation">The parent directory or new directory is already open in not compatible sharing mode.</see>
    /// <see cref="FileSystemErrorCode.UnauthorizedAccess">Access has been denied.</see>
    /// </exception>
    Task<NodeInfo<TId>> CreateDirectory(NodeInfo<TId> info, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new file.
    /// </summary>
    /// <param name="info">The new file information.</param>
    /// <param name="tempFileName">The name of a temporary file to use while writing file content.
    /// Null or empty string indicates to write directly to the destination file.</param>
    /// <param name="thumbnailProvider">An object that will provide a thumbnail for the file.</param>
    /// <param name="progressCallback">TODO</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="IRevisionCreationProcess{TId}"/> used for writing the file content and finishing
    /// the file creation.</returns>
    /// <remarks>
    /// The expected content of the <paramref name="info"/> argument:
    /// <list type="bullet">
    /// <item><term>Path</term><description>The path of the file to create.</description></item>
    /// <item><term>ParentId</term><description>The parent directory identity.</description></item>
    /// <item><term>Name</term><description>The name of the file to create.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="FileSystemClientException{TId}">File system client specific exception has occurred.
    /// Expected specific <see cref="FileSystemClientException.ErrorCode"/> values:
    /// <see cref="FileSystemErrorCode.ObjectNotFound">The parent directory does not exist.</see>
    /// <see cref="FileSystemErrorCode.PathNotFound">The parent directory cannot be found at the specified path.</see>
    /// <see cref="FileSystemErrorCode.DirectoryNotFound">The part of the parent directory path does not exist.</see>
    /// <see cref="FileSystemErrorCode.IdentityMismatch">The parent directory identity does not match the expected.</see>
    /// <see cref="FileSystemErrorCode.MetadataMismatch">The parent file system object is a file.</see>
    /// <see cref="FileSystemErrorCode.DuplicateName">The file system object with the specified name already exists on the parent directory.</see>
    /// <see cref="FileSystemErrorCode.SharingViolation">The parent directory is already open in not compatible sharing mode.</see>
    /// <see cref="FileSystemErrorCode.UnauthorizedAccess">Access has been denied.</see>
    /// <see cref="FileSystemErrorCode.FreeSpaceExceeded">The file cannot be created due to the lack of free space.</see>
    /// </exception>
    Task<IRevisionCreationProcess<TId>> CreateFile(
        NodeInfo<TId> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens the specified file for reading.
    /// </summary>
    /// <param name="info">The file information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="IRevision"/> for reading the file content, thumbnail, and other properties.</returns>
    /// <remarks>
    /// The expected content of the <paramref name="info"/> argument:
    /// <list type="bullet">
    /// <item><term>Path</term><description>The file path.</description></item>
    /// <item><term>Id</term><description>The file identity.</description></item>
    /// <item><term>Name</term><description>The expected file name.</description></item>
    /// <item><term>LastWriteTimeUtc</term><description>The expected file last write time or default.</description></item>
    /// <item><term>Size</term><description>The expected file size or -1.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="FileSystemClientException{TId}">File system client specific exception has occurred.
    /// Expected specific <see cref="FileSystemClientException.ErrorCode"/> values:
    /// <see cref="FileSystemErrorCode.ObjectNotFound">The file does not exist.</see>
    /// <see cref="FileSystemErrorCode.PathNotFound">The file cannot be found at the specified path.</see>
    /// <see cref="FileSystemErrorCode.DirectoryNotFound">The part of the file path does not exist.</see>
    /// <see cref="FileSystemErrorCode.IdentityMismatch">The file identity does not match the expected.</see>
    /// <see cref="FileSystemErrorCode.MetadataMismatch">The file metadata (type, name, file revision, last write time, or size) differs from the expected.</see>
    /// <see cref="FileSystemErrorCode.SharingViolation">The file is already open in not compatible sharing mode.</see>
    /// <see cref="FileSystemErrorCode.UnauthorizedAccess">Access has been denied.</see>
    /// </exception>
    Task<IRevision> OpenFileForReading(NodeInfo<TId> info, CancellationToken cancellationToken);

    /// <summary>
    /// Opens the specified file for writing.
    /// </summary>
    /// <param name="info">The file information.</param>
    /// <param name="size">The file size of the new file to write.</param>
    /// <param name="lastWriteTime">The new last write time value.</param>
    /// <param name="tempFileName">The name of a temporary file to use while uploading file content.
    /// Null or empty string indicates to write directly to the destination file.</param>
    /// <param name="thumbnailProvider">An object that will provide a thumbnail for the file.</param>
    /// <param name="progressCallback">TODO</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="IRevisionCreationProcess{TId}"/> used for writing the file content.</returns>
    /// <remarks>
    /// The expected content of the <paramref name="info"/> argument:
    /// <list type="bullet">
    /// <item><term>Path</term><description>The file path.</description></item>
    /// <item><term>Id</term><description>The file identity.</description></item>
    /// <item><term>Name</term><description>The expected file name.</description></item>
    /// <item><term>LastWriteTimeUtc</term><description>The expected file last write time or default.</description></item>
    /// <item><term>Size</term><description>The expected file size or -1.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="FileSystemClientException{TId}">File system client specific exception has occurred.
    /// Expected specific <see cref="FileSystemClientException.ErrorCode"/> values:
    /// <see cref="FileSystemErrorCode.ObjectNotFound">The file does not exist.</see>
    /// <see cref="FileSystemErrorCode.PathNotFound">The file cannot be found at the specified path.</see>
    /// <see cref="FileSystemErrorCode.DirectoryNotFound">The part of the file path does not exist.</see>
    /// <see cref="FileSystemErrorCode.IdentityMismatch">The file identity does not match the expected.</see>
    /// <see cref="FileSystemErrorCode.MetadataMismatch">The file metadata (type, name, file revision, last write time, or size) differs from the expected.</see>
    /// <see cref="FileSystemErrorCode.SharingViolation">The file is already open in not compatible sharing mode.</see>
    /// <see cref="FileSystemErrorCode.UnauthorizedAccess">Access has been denied.</see>
    /// </exception>
    Task<IRevisionCreationProcess<TId>> CreateRevision(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken);

    /// <summary>
    /// Renames and/or moves the file system object to the new parent directory.
    /// </summary>
    /// <param name="info">The file system object information.</param>
    /// <param name="destinationInfo">The new file system object information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The expected content of the <paramref name="info"/> argument:
    /// <list type="bullet">
    /// <item><term>Path</term><description>The file system object path.</description></item>
    /// <item><term>Id</term><description>The file system object identity.</description></item>
    /// <item><term>ParentId</term><description>The parent directory identity.</description></item>
    /// <item><term>Name</term><description>The expected file system object name.</description></item>
    /// <item><term>Attributes</term><description>The expected file system object type.</description></item>
    /// <item><term>LastWriteTimeUtc</term><description>The expected file system object last write time or default.</description></item>
    /// <item><term>Size</term><description>The expected file size or -1. For files only.</description></item>
    /// </list>
    /// The expected content of the <paramref name="destinationInfo"/> argument:
    /// <list type="bullet">
    /// <item><term>Path</term><description>The new file system object path. If not specified, assumed rename only.</description></item>
    /// <item><term>ParentId</term><description>The new parent directory identity.</description></item>
    /// <item><term>Name</term><description>The new file system object name.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="FileSystemClientException{TId}">File system client specific exception has occurred.
    /// Expected specific <see cref="FileSystemClientException.ErrorCode"/> values:
    /// <see cref="FileSystemErrorCode.ObjectNotFound">The file system object or new parent directory does not exist.</see>
    /// <see cref="FileSystemErrorCode.PathNotFound">The file system object or new parent directory cannot be found at the specified path.</see>
    /// <see cref="FileSystemErrorCode.DirectoryNotFound">The part of the file system object or the new parent directory path does not exist.</see>
    /// <see cref="FileSystemErrorCode.IdentityMismatch">The file system object or new parent directory identity does not match the expected.</see>
    /// <see cref="FileSystemErrorCode.MetadataMismatch">The file system object metadata (type, name, file revision, last write time, or file size)
    /// differs from the expected or new parent is a file.</see>
    /// <see cref="FileSystemErrorCode.DuplicateName">The file system object with the specified name already exists on the new parent directory.</see>
    /// <see cref="FileSystemErrorCode.SharingViolation">The file system object or the new parent directory is already open in not compatible sharing mode.</see>
    /// <see cref="FileSystemErrorCode.UnauthorizedAccess">Access has been denied.</see>
    /// </exception>
    Task Move(NodeInfo<TId> info, NodeInfo<TId> destinationInfo, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the specified file system object. Directories are deleted recursively including all descendants.
    /// </summary>
    /// <param name="info">The file system object information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The expected content of the <paramref name="info"/> argument:
    /// <list type="bullet">
    /// <item><term>Path</term><description>The file system object path.</description></item>
    /// <item><term>Id</term><description>The file system object identity.</description></item>
    /// <item><term>ParentId</term><description>The parent directory identity.</description></item>
    /// <item><term>Name</term><description>The expected file system object name.</description></item>
    /// <item><term>Attributes</term><description>The expected file system object type.</description></item>
    /// <item><term>LastWriteTimeUtc</term><description>The expected file system object last write time.</description></item>
    /// <item><term>Size</term><description>The expected file size. For files only.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="FileSystemClientException{TId}">File system client specific exception has occurred.
    /// Expected specific <see cref="FileSystemClientException.ErrorCode"/> values:
    /// <see cref="FileSystemErrorCode.ObjectNotFound">The file system object does not exist.</see>
    /// <see cref="FileSystemErrorCode.PathNotFound">The file system object cannot be found at the specified path.</see>
    /// <see cref="FileSystemErrorCode.DirectoryNotFound">The part of the file system object path does not exist.</see>
    /// <see cref="FileSystemErrorCode.IdentityMismatch">The file system object identity does not match the expected.</see>
    /// <see cref="FileSystemErrorCode.MetadataMismatch">The file system object metadata (type, name, file revision, last write time, or file size) differs from the expected.</see>
    /// <see cref="FileSystemErrorCode.SharingViolation">The file system object is already open in not compatible sharing mode.</see>
    /// <see cref="FileSystemErrorCode.UnauthorizedAccess">Access has been denied.</see>
    /// </exception>
    Task Delete(NodeInfo<TId> info, CancellationToken cancellationToken);

    /// <summary>
    /// Permanently deletes the specified file system object. Directories are deleted recursively including all descendants.
    /// </summary>
    /// <param name="info">The file system object information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The expected content of the <paramref name="info"/> argument:
    /// <list type="bullet">
    /// <item><term>Path</term><description>The file system object path.</description></item>
    /// <item><term>Id</term><description>The file system object identity.</description></item>
    /// <item><term>ParentId</term><description>The parent directory identity.</description></item>
    /// <item><term>Name</term><description>The expected file system object name.</description></item>
    /// <item><term>Attributes</term><description>The expected file system object type.</description></item>
    /// <item><term>LastWriteTimeUtc</term><description>The expected file system object last write time.</description></item>
    /// <item><term>Size</term><description>The expected file size. For files only.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="FileSystemClientException{TId}">File system client specific exception has occurred.
    /// Expected specific <see cref="FileSystemClientException.ErrorCode"/> values:
    /// <see cref="FileSystemErrorCode.ObjectNotFound">The file system object does not exist.</see>
    /// <see cref="FileSystemErrorCode.PathNotFound">The file system object cannot be found at the specified path.</see>
    /// <see cref="FileSystemErrorCode.DirectoryNotFound">The part of the file system object path does not exist.</see>
    /// <see cref="FileSystemErrorCode.IdentityMismatch">The file system object identity does not match the expected.</see>
    /// <see cref="FileSystemErrorCode.MetadataMismatch">The file system object metadata (type, name, file revision, last write time, or file size) differs from the expected.</see>
    /// <see cref="FileSystemErrorCode.SharingViolation">The file system object is already open in not compatible sharing mode.</see>
    /// <see cref="FileSystemErrorCode.UnauthorizedAccess">Access has been denied.</see>
    /// </exception>
    Task DeletePermanently(NodeInfo<TId> info, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the specified file revision. Not supported on local file systems.
    /// </summary>
    /// <param name="info">The file revision information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The expected content of the <paramref name="info"/> argument:
    /// <list type="bullet">
    /// <item><term>Id</term><description>The file identity. Required.</description></item>
    /// <item><term>ParentId</term><description>The parent directory identity. Optional.</description></item>
    /// <item><term>RevisionId</term><description>The file revision identity. Required.</description></item>
    /// <item><term>Name</term><description>The expected file name. Optional.</description></item>
    /// <item><term>Attributes</term><description>The expected file system object type, that must be file. Required.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="FileSystemClientException{TId}">File system client specific exception has occurred.
    /// Expected specific <see cref="FileSystemClientException.ErrorCode"/> values:
    /// <see cref="FileSystemErrorCode.ObjectNotFound">The file does not exist.</see>
    /// <see cref="FileSystemErrorCode.MetadataMismatch">The file system object metadata (type, name) differs from the expected.</see>
    /// <see cref="FileSystemErrorCode.UnauthorizedAccess">Access has been denied.</see>
    /// </exception>
    Task DeleteRevision(NodeInfo<TId> info, CancellationToken cancellationToken);

    void SetInSyncState(NodeInfo<TId> info);

    Task HydrateFileAsync(NodeInfo<TId> info, CancellationToken cancellationToken);
}
