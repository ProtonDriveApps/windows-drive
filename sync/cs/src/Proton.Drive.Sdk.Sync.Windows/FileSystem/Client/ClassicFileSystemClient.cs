using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Metrics;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Client;

/// <summary>
/// Classic local File System Client for Windows.
/// </summary>
internal sealed class ClassicFileSystemClient : FileSystemClientBase, IFileSystemClient<long>
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = default,
        ReturnSpecialDirectories = false,
        BufferSize = 16_384,
    };

    private static readonly EnumerationOptions GetInfoOptions = new()
    {
        AttributesToSkip = default,
        ReturnSpecialDirectories = false,
        BufferSize = 1_024,
    };

    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly IFileMetadataGenerator _fileMetadataGenerator;
    private readonly IPhotoTagsGenerator _photoTagsGenerator;
    private readonly Action<MetricEvent> _recordMetricEvent;

    public ClassicFileSystemClient(
        IFeatureFlagProvider featureFlagProvider,
        IThumbnailGenerator thumbnailGenerator,
        IFileMetadataGenerator fileMetadataGenerator,
        IPhotoTagsGenerator photoTagsGenerator,
        Action<MetricEvent> recordMetricEvent)
    {
        _featureFlagProvider = featureFlagProvider;
        _thumbnailGenerator = thumbnailGenerator;
        _fileMetadataGenerator = fileMetadataGenerator;
        _photoTagsGenerator = photoTagsGenerator;
        _recordMetricEvent = recordMetricEvent;
    }

    public void Connect(string syncRootPath, IFileHydrationDemandHandler<long> fileHydrationDemandHandler)
    {
        // Do nothing
    }

    public Task DisconnectAsync()
    {
        return Task.CompletedTask;
    }

    public Task<NodeInfo<long>> GetInfoAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var parentDirectory = OpenAndCheckParentDirectory(info);

        var fileName = !string.IsNullOrEmpty(info.Name) ? info.Name : Path.GetFileName(info.Path);

        if (string.IsNullOrEmpty(fileName))
        {
            // This is the root directory, it has no parent and no name
            CheckIdentity(parentDirectory, info);

            return Task.FromResult(ToNodeInfo(parentDirectory, refresh: false));
        }

        var fsEntry = WithMappedException(parentDirectory.EnumerateFileSystemEntries(fileName, GetInfoOptions, ownsHandle: false), info.Id)
            .SingleOrDefault();

        if (fsEntry == null)
        {
            throw new FileSystemClientException<long>(
                "Could not find file system object at the provided path",
                FileSystemErrorCode.PathNotFound,
                info.Id);
        }

        CheckIdentity(fsEntry, info);

        return Task.FromResult(ToNodeInfo(fsEntry).WithParentId(parentDirectory.ObjectId));
    }

    public IAsyncEnumerable<NodeInfo<long>> EnumerateAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var directory = OpenDirectory(
            info,
            FileSystemFileAccess.ReadData,
            FileShare.ReadWrite);

        CheckIsDirectory(directory, info.Id);
        CheckIdentity(directory, info);

        var parentId = directory.ObjectId;

        return WithMappedException(directory.EnumerateFileSystemEntries(fileName: null, EnumerationOptions, ownsHandle: true), info.Id)
            .Select(x => x.ToNodeInfo(parentId))
            .ToAsyncEnumerable();
    }

    public Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(info.Path, nameof(info), nameof(info.Path));

        cancellationToken.ThrowIfCancellationRequested();

        var file = OpenFile(
            info,
            FileMode.Open,
            FileSystemFileAccess.ReadData,
            FileShare.ReadWrite | FileShare.Delete);

        try
        {
            CheckIsFile(file, info.Id);
            CheckIdentity(file, info);
            CheckMetadata(file, info);

            return Task.FromResult((ISourceRevision)new LocalFileRevision(file, _thumbnailGenerator, _fileMetadataGenerator, _photoTagsGenerator));
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    public Task<NodeInfo<long>> CreateDirectoryAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(info.Path, nameof(info), nameof(info.Path));

        cancellationToken.ThrowIfCancellationRequested();

        info.ThrowIfNameIsInvalid();

        using var parentDirectory = OpenAndCheckParentDirectory(info);

        CreateDirectoryInternal(info);

        using var directory = OpenDirectory(
            info,
            FileSystemFileAccess.ReadAttributes | FileSystemFileAccess.WriteAttributes,
            FileShare.ReadWrite | FileShare.Delete);

        CheckIsDirectory(directory, 0);

        directory.SetAttributes(info);

        return Task.FromResult(directory.ToNodeInfo(parentDirectory.ObjectId, refresh: true));
    }

    public async Task<IDestinationRevision<long>> CreateFileAsync(
        NodeInfo<long> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(info.Path, nameof(info), nameof(info.Path));

        cancellationToken.ThrowIfCancellationRequested();

        info.ThrowIfNameIsInvalid();

        using var parentDirectory = OpenAndCheckParentDirectory(info);

        cancellationToken.ThrowIfCancellationRequested();

        var tempInfo = info;
        FileAttributes tempFileAttributes = default;

        if (!string.IsNullOrEmpty(tempFileName))
        {
            // Create and immediately delete the file to ensure the file name is not used
            // before creating the temporary file.
            using (var fileForCheckingNameAvailability = OpenFile(
                       info,
                       FileMode.CreateNew,
                       FileSystemFileAccess.Delete,
                       FileShare.Delete,
                       FileAttributes.Hidden,
                       FileOptions.DeleteOnClose))
            {
                // To make less confusion, renaming the temporary file before deletion ensures the name
                // of the file being created does not appear mentioned in the logs as deleted.
                // ReSharper disable once AccessToDisposedClosure
                WithMappedException(() => fileForCheckingNameAvailability.Rename(tempFileName), 0);
            }

            tempInfo = info.ToTempFileInfo(tempFileName);
            tempFileAttributes = FileAttributes.Hidden;

            cancellationToken.ThrowIfCancellationRequested();
        }

        var file = OpenFile(
            tempInfo,
            FileMode.CreateNew,
            FileSystemFileAccess.ReadWrite | FileSystemFileAccess.Delete,
            FileShare.Read,
            info.Attributes | tempFileAttributes);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            tempInfo = file.ToNodeInfo(parentDirectory.ObjectId, refresh: false).WithPath(tempInfo.Path);
            var finalInfo = info.Copy().WithParentId(parentDirectory.ObjectId);
            var checksumVerificationEnabled = await _featureFlagProvider.DownloadChecksumVerificationIsEnabledAsync(cancellationToken).ConfigureAwait(false);

            return new ClassicRevisionCreationProcess(
                file,
                initialInfo: null,
                tempInfo,
                finalInfo,
                checksumVerificationEnabled,
                progressCallback,
                _recordMetricEvent);
        }
        catch
        {
            file.TryDelete();
            file.Dispose();
            throw;
        }
    }

    public async Task<IDestinationRevision<long>> CreateRevisionAsync(
        NodeInfo<long> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(info.Path, nameof(info), nameof(info.Path));

        cancellationToken.ThrowIfCancellationRequested();

        // Archive attribute indicates the file should be backed up before overwriting. It's not used here.
        info = info.Copy().WithAttributes(info.Attributes & ~FileAttributes.Archive);

        FileSystemFile file;
        FileAttributes fileAttributes;

        if (!string.IsNullOrEmpty(tempFileName))
        {
            // Open and check the file to ensure it exists, is writable, and has not diverged metadata
            using var originalFile = info.OpenAsFile(
                FileMode.Open,
                FileSystemFileAccess.ReadAttributes | FileSystemFileAccess.WriteData | FileSystemFileAccess.Delete,
                FileShare.Read | FileShare.Delete);

            originalFile.ThrowIfMetadataMismatch(info);

            cancellationToken.ThrowIfCancellationRequested();

            fileAttributes = originalFile.Attributes;
            var tempInfo = info.ToTempFileInfo(tempFileName);

            file = tempInfo.CreateTemporaryFile(originalFile);
        }
        else
        {
            file = info.OpenAsFileForWriting();
            fileAttributes = file.Attributes;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = file.ToNodeInfo(parentId: 0, refresh: false).WithSize(0).WithPath(file.FullPath);

            var checksumVerificationEnabled = await _featureFlagProvider.DownloadChecksumVerificationIsEnabledAsync(cancellationToken).ConfigureAwait(false);

            return new ClassicRevisionCreationProcess(
                file,
                info,
                fileInfo,
                fileInfo.Copy().WithName(info.Name).WithPath(info.Path).WithAttributes(fileAttributes).WithSize(size).WithLastWriteTimeUtc(lastWriteTime),
                checksumVerificationEnabled,
                progressCallback,
                _recordMetricEvent);
        }
        catch
        {
            file.TryDelete();
            file.Dispose();
            throw;
        }
    }

    public Task MoveAsync(NodeInfo<long> info, NodeInfo<long> destinationInfo, CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(info.Path, nameof(info), nameof(info.Path));
        Ensure.IsFalse(
            string.IsNullOrEmpty(destinationInfo.Path) && string.IsNullOrEmpty(destinationInfo.Name),
            $"Both {nameof(destinationInfo)}.{nameof(destinationInfo.Path)} and {nameof(destinationInfo)}.{nameof(destinationInfo.Name)} cannot be null or empty.");
        Ensure.IsFalse(
            ((info.Attributes ^ destinationInfo.Attributes) & FileAttributes.Directory) != 0,
            $"Both {nameof(info)}.{nameof(info.Attributes)} and {nameof(destinationInfo)}.{nameof(destinationInfo.Attributes)} must either have {nameof(FileAttributes.Directory)} flag or not have it");

        cancellationToken.ThrowIfCancellationRequested();

        var newName = destinationInfo.GetNameAndThrowIfInvalid();

        using var fsObject = OpenFileSystemObject(
            info,
            FileSystemFileAccess.ReadAttributes | FileSystemFileAccess.Delete,
            FileShare.Read | FileShare.Delete);

        CheckType(fsObject, info);
        CheckIdentity(fsObject, info);
        CheckMetadata(fsObject, info);

        if (IsRename(destinationInfo))
        {
            fsObject.Rename(newName, includeObjectId: true);
        }
        else
        {
            using var newParent = OpenAndCheckParentDirectory(
                destinationInfo,
                FileSystemFileAccess.TraverseDirectory | FileSystemFileAccess.ReadAttributes);

            MoveInternal(fsObject, newParent, newName, info.Id);
        }

        return Task.CompletedTask;
    }

    public Task MoveAsync(IReadOnlyList<NodeInfo<long>> sourceNodes, NodeInfo<long> destinationInfo, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task DeleteRevisionAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public void SetInSyncState(NodeInfo<long> info)
    {
        // Do nothing
    }

    public Task HydrateFileAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        // Do nothing
        return Task.CompletedTask;
    }

    private FileSystemDirectory OpenAndCheckParentDirectory(
        NodeInfo<long> info,
        FileSystemFileAccess access = FileSystemFileAccess.ReadAttributes | FileSystemFileAccess.ReadData,
        FileShare share = FileShare.ReadWrite)
    {
        var path = Path.GetDirectoryName(info.Path) ?? info.Path;

        var directory = WithMappedException(() => FileSystemDirectory.Open(path, access, share), info.ParentId);

        try
        {
            CheckIsDirectory(directory, info.ParentId);
            CheckParentIdentity(directory, info);

            return directory;
        }
        catch
        {
            directory.Dispose();
            throw;
        }
    }

    private FileSystemObject OpenFileSystemObject(NodeInfo<long> info, FileSystemFileAccess access, FileShare share)
    {
        return WithMappedException(() => FileSystemObject.Open(info.Path, FileMode.Open, access, share, FileOptions.None), info.Id);
    }

    private FileSystemFile OpenFile(
        NodeInfo<long> info,
        FileMode mode,
        FileSystemFileAccess access,
        FileShare share,
        FileAttributes attributes = default,
        FileOptions options = FileOptions.Asynchronous,
        FileSystemFile? templateFile = null)
    {
        return WithMappedException(() => FileSystemFile.Open(info.Path, mode, access, share, attributes, options, templateFile?.FileHandle), info.Id);
    }

    private FileSystemDirectory OpenDirectory(NodeInfo<long> info, FileSystemFileAccess access, FileShare share)
    {
        return WithMappedException(() => FileSystemDirectory.Open(info.Path, access, share, FileOptions.None), info.Id);
    }

    private void CreateDirectoryInternal(NodeInfo<long> info)
    {
        WithMappedException(() => FileSystemDirectory.Create(info.Path), 0);
    }

    private bool IsRename(NodeInfo<long> newInfo)
    {
        return string.IsNullOrEmpty(newInfo.Path);
    }

    private void MoveInternal(FileSystemObject fsObject, FileSystemDirectory newParent, string newName, long id, bool replaceIfExists = false)
    {
        WithMappedException(
            () => WithMappedInvalidNameException(
                () => fsObject.Move(newParent, newName, replaceIfExists),
                id),
            id);
    }

    private void CheckParentIdentity(FileSystemObject fsObject, NodeInfo<long> info)
    {
        CheckIdentity(fsObject, info.ParentId);
    }

    private void CheckIdentity(FileSystemObject fsObject, NodeInfo<long> info)
    {
        CheckIdentity(fsObject, info.Id);
    }

    private void CheckIdentity(FileSystemObject fsObject, long id)
    {
        WithMappedException(UnsafeCheckIdentity, id);

        void UnsafeCheckIdentity()
        {
            if (!id.Equals(0) && !fsObject.ObjectId.Equals(id))
            {
                throw new FileSystemClientException<long>(
                    $"Wrong file system object ID, expected {id} but found {fsObject.ObjectId}",
                    FileSystemErrorCode.IdentityMismatch,
                    id);
            }
        }
    }

    private void CheckIdentity(FileSystemEntry fsEntry, NodeInfo<long> info)
    {
        if (!info.Id.Equals(0) && !fsEntry.ObjectId.Equals(info.Id))
        {
            throw new FileSystemClientException<long>(
                $"Wrong file system object ID, expected {info.Id} but found {fsEntry.ObjectId}",
                FileSystemErrorCode.IdentityMismatch,
                info.Id);
        }
    }

    private void CheckType(FileSystemObject fsObject, NodeInfo<long> info)
    {
        WithMappedException(UnsafeCheckType, info.Id);

        void UnsafeCheckType()
        {
            if (((fsObject.Attributes ^ info.Attributes) & FileAttributes.Directory) != 0)
            {
                throw new FileSystemClientException<long>(
                    $"Wrong file system object type, expected {ObjectType(info.Attributes)} but found {ObjectType(fsObject.Attributes)}",
                    FileSystemErrorCode.MetadataMismatch,
                    info.Id);
            }

            string ObjectType(FileAttributes attributes) =>
                (attributes & FileAttributes.Directory) != 0 ? "Directory" : "File";
        }
    }

    private void CheckMetadata(FileSystemObject fsObject, NodeInfo<long> info)
    {
        WithMappedException(UnsafeCheckMetadata, info.Id);

        void UnsafeCheckMetadata()
        {
            if (info.LastWriteTimeUtc != DateTime.MinValue && fsObject.LastWriteTimeUtc != info.LastWriteTimeUtc)
            {
                throw new FileSystemClientException<long>(
                    "Local optimistic locking failure: Last write time has diverged",
                    FileSystemErrorCode.MetadataMismatch,
                    info.Id);
            }

            if ((fsObject.Attributes & FileAttributes.Directory) != 0)
            {
                return;
            }

            if (info.Size >= 0 && fsObject.Size != info.Size)
            {
                throw new FileSystemClientException<long>(
                    "Local optimistic locking failure: File size has diverged",
                    FileSystemErrorCode.MetadataMismatch,
                    info.Id);
            }
        }
    }

    private void CheckIsDirectory(FileSystemObject fsObject, long id)
    {
        WithMappedException(UnsafeCheckIsDirectory, id);

        void UnsafeCheckIsDirectory()
        {
            if ((fsObject.Attributes & FileAttributes.Directory) == 0)
            {
                throw new FileSystemClientException<long>(
                    $"The file system object with Id={fsObject.ObjectId} is not a directory",
                    FileSystemErrorCode.MetadataMismatch,
                    id);
            }
        }
    }

    private void CheckIsFile(FileSystemObject fsObject, long id)
    {
        WithMappedException(UnsafeCheckIsFile, id);

        void UnsafeCheckIsFile()
        {
            if ((fsObject.Attributes & FileAttributes.Directory) != 0)
            {
                throw new FileSystemClientException<long>(
                    $"The file system object with Id={fsObject.ObjectId} is not a file",
                    FileSystemErrorCode.MetadataMismatch,
                    id);
            }
        }
    }

    private NodeInfo<long> ToNodeInfo(FileSystemObject fsObject, bool refresh)
    {
        return WithMappedException(UnsafeToNodeInfo, id: 0);

        NodeInfo<long> UnsafeToNodeInfo()
        {
            if (refresh)
            {
                fsObject.Refresh();
            }

            return new NodeInfo<long>()
                .WithId(fsObject.ObjectId)
                .WithName(fsObject.Name)
                .WithAttributes(fsObject.Attributes)
                .WithLastWriteTimeUtc(fsObject.LastWriteTimeUtc)
                .WithSize(fsObject.Size)
                .WithPlaceholderState(fsObject.GetPlaceholderState());
        }
    }

    private NodeInfo<long> ToNodeInfo(FileSystemEntry entry)
    {
        return new NodeInfo<long>()
            .WithId(entry.ObjectId)
            .WithName(entry.Name)
            .WithAttributes(entry.Attributes)
            .WithLastWriteTimeUtc(entry.LastWriteTimeUtc)
            .WithSize(entry.Size)
            .WithPlaceholderState(entry.PlaceholderState);
    }

    private IEnumerable<T> WithMappedException<T>(IEnumerable<T> origin, long id)
    {
        using var enumerator = WithMappedException(origin.GetEnumerator, id);

        while (true)
        {
            if (!WithMappedException(enumerator.MoveNext, id))
            {
                yield break;
            }

            // ReSharper disable once AccessToDisposedClosure
            yield return WithMappedException(() => enumerator.Current, id);
        }
    }

    private void WithMappedInvalidNameException(Action origin, long id)
    {
        try
        {
            origin();
        }
        catch (ArgumentException ex)
        {
            throw new FileSystemClientException<long>(FileSystemErrorCode.InvalidName, id, ex);
        }
    }

    private void WithMappedException(Action origin, long id)
    {
        try
        {
            origin();
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, id, id != 0, out var mappedException))
        {
            throw mappedException;
        }
    }

    private T WithMappedException<T>(Func<T> origin, long id)
    {
        try
        {
            return origin();
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, id, id != 0, out var mappedException))
        {
            throw mappedException;
        }
    }
}
