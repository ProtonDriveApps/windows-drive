using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;
using Proton.Drive.Sdk.Sync.Windows.FileSystem.Client.CloudFiles;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Metrics;
using static Vanara.PInvoke.CldApi;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Client;

internal sealed class OnDemandHydrationFileSystemClient : FileSystemClientBase, IFileSystemClient<long>
{
    private static readonly EnumerationOptions DefaultEnumerationOptions = new()
    {
        AttributesToSkip = default,
        ReturnSpecialDirectories = false,
        BufferSize = 16_384,
    };

    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly IFileMetadataGenerator _fileMetadataGenerator;
    private readonly IPhotoTagsGenerator _photoTagsGenerator;
    private readonly ILocalVolumeInfoProvider _localVolumeInfoProvider;
    private readonly Action<MetricEvent> _recordMetric;
    private readonly ILoggerFactory _loggerFactory;
    private readonly EnumerationOptions _enumerationOptions;
    private string? _currentConnectionRootPath;
    private int _numberOfConnections;

    private SyncRootConnection? _syncRootConnection;
    private SyncRootCallbackDispatcher? _syncRootCallbackDispatcher;

    public OnDemandHydrationFileSystemClient(
        IFeatureFlagProvider featureFlagProvider,
        IThumbnailGenerator thumbnailGenerator,
        IFileMetadataGenerator fileMetadataGenerator,
        IPhotoTagsGenerator photoTagsGenerator,
        ILocalVolumeInfoProvider localVolumeInfoProvider,
        Action<MetricEvent> recordMetric,
        ILoggerFactory loggerFactory,
        EnumerationOptions? enumerationOptions = null)
    {
        _featureFlagProvider = featureFlagProvider;
        _thumbnailGenerator = thumbnailGenerator;
        _fileMetadataGenerator = fileMetadataGenerator;
        _photoTagsGenerator = photoTagsGenerator;
        _localVolumeInfoProvider = localVolumeInfoProvider;
        _recordMetric = recordMetric;
        _loggerFactory = loggerFactory;
        _enumerationOptions = enumerationOptions ?? DefaultEnumerationOptions;
    }

    public void Connect(string syncRootPath, IFileHydrationDemandHandler<long> fileHydrationDemandHandler)
    {
        if (_currentConnectionRootPath is not null
            && !string.Equals(_currentConnectionRootPath, syncRootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new FileSystemClientException("Attempted to connect to different sync root path while connection is active");
        }

        ++_numberOfConnections;

        if (_numberOfConnections > 1)
        {
            return;
        }

        try
        {
            try
            {
                _syncRootCallbackDispatcher = new SyncRootCallbackDispatcher(
                    fileHydrationDemandHandler,
                    _featureFlagProvider,
                    _localVolumeInfoProvider,
                    _recordMetric,
                    _loggerFactory.CreateLogger<SyncRootCallbackDispatcher>());

                _syncRootConnection = SyncRoot.Connect(syncRootPath, _syncRootCallbackDispatcher);
                _currentConnectionRootPath = syncRootPath;
            }
            catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
            {
                throw new FileSystemClientException("Failed to connect to the on-demand sync root", ex);
            }
        }
        catch
        {
            --_numberOfConnections;
        }
    }

    public async Task DisconnectAsync()
    {
        --_numberOfConnections;

        if (_numberOfConnections > 0)
        {
            return;
        }

        try
        {
            try
            {
                var syncRootCallbackDispatcher = Interlocked.Exchange(ref _syncRootCallbackDispatcher, null);
                if (syncRootCallbackDispatcher is not null)
                {
                    await syncRootCallbackDispatcher.DisposeAsync().ConfigureAwait(false);
                }

                _syncRootConnection?.Dispose();
                _syncRootConnection = null;
                _currentConnectionRootPath = null;
            }
            catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
            {
                throw new FileSystemClientException("Failed to disconnect from the on-demand sync root", ex);
            }
        }
        catch
        {
            ++_numberOfConnections;
        }
    }

    public Task<NodeInfo<long>> GetInfoAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = FileSystemNodeInfoProvider.Convert(info);

        return Task.FromResult(result);
    }

    public IAsyncEnumerable<NodeInfo<long>> EnumerateAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var directory = info.OpenAsDirectory(FileSystemFileAccess.ReadData, FileShare.ReadWrite);

        var parentId = directory.ObjectId;

        var entries = directory.EnumerateFileSystemEntries(fileName: null, _enumerationOptions, ownsHandle: true).WithExceptionMapping(info.Id);

        return entries.Select(x => x.ToNodeInfo(parentId)).ToAsyncEnumerable();
    }

    public Task<NodeInfo<long>> CreateDirectoryAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(info.Path, nameof(info), nameof(info.Path));

        cancellationToken.ThrowIfCancellationRequested();

        info.ThrowIfNameIsInvalid();

        // The parent directory is opened and kept open during child directory creation.
        // This ensures the directory is created at the desired parent directory.
        using var parentDirectory = info.OpenParentDirectory(FileSystemFileAccess.ReadAttributes | FileSystemFileAccess.ReadData, FileShare.ReadWrite);

        try
        {
            FileSystemDirectory.Create(info.Path);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, id: null, out var mappedException))
        {
            throw mappedException;
        }

        using var directory = info.OpenAsDirectory(
            FileSystemFileAccess.ReadAttributes | FileSystemFileAccess.WriteAttributes,
            FileShare.ReadWrite | FileShare.Delete);

        directory.SetAttributes(info);

        if ((info.Attributes & FileAttributes.Hidden) > 0)
        {
            directory.SetPinState(CF_PIN_STATE.CF_PIN_STATE_EXCLUDED, CF_SET_PIN_FLAGS.CF_SET_PIN_FLAG_RECURSE);
        }

        return Task.FromResult(directory.ToNodeInfo(parentDirectory.ObjectId, refresh: false));
    }

    public Task<IDestinationRevision<long>> CreateFileAsync(
        NodeInfo<long> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(info.Path, nameof(info), nameof(info.Path));
        Ensure.IsTrue(info.Size >= 0, "Size must be specified");

        cancellationToken.ThrowIfCancellationRequested();

        info.ThrowIfNameIsInvalid();

        // The parent directory is opened and kept open during child file creation.
        // This ensures the file is created at the desired parent directory.
        // Also, this makes it fail early if parent directory cannot be opened.
        var parentDirectory = info.OpenParentDirectory(FileSystemFileAccess.ReadAttributes | FileSystemFileAccess.ReadData, FileShare.ReadWrite);
        try
        {
            var result = new OnDemandFileCreationProcess(info, parentDirectory);

            return Task.FromResult<IDestinationRevision<long>>(result);
        }
        catch
        {
            parentDirectory.Dispose();
            throw;
        }
    }

    public Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(info.Path, nameof(info), nameof(info.Path));

        cancellationToken.ThrowIfCancellationRequested();

        // Opening the file for reading data would trigger hydration of partial placeholder
        using var file = info.OpenAsFile(FileMode.Open, FileSystemFileAccess.ReadAttributes, FileShare.Read);

        file.ThrowIfMetadataMismatch(info);
        file.ThrowIfPartial(info.Id);

        var fileForReadingData = info.OpenAsFile(FileMode.Open, FileSystemFileAccess.ReadData, FileShare.ReadWrite | FileShare.Delete);

        try
        {
            return Task.FromResult((ISourceRevision)new LocalFileRevision(fileForReadingData, _thumbnailGenerator, _fileMetadataGenerator, _photoTagsGenerator));
        }
        catch
        {
            fileForReadingData.Dispose();
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
        Ensure.NotNullOrEmpty(tempFileName, nameof(tempFileName));

        cancellationToken.ThrowIfCancellationRequested();

        // Archive attribute indicates the file should be backed up before overwriting
        var backup = info.Attributes.HasFlag(FileAttributes.Archive);
        info = info.Copy().WithAttributes(info.Attributes & ~FileAttributes.Archive);

        // Read-only attribute indicates a request to overwrite a read-only file
        var overwriteReadOnly = info.Attributes.HasFlag(FileAttributes.ReadOnly);

        // Makes it fail early if the file cannot be opened for writing data.
        // If overwriting a read-only file, we do not attempt to open file for writing data, as it would fail.
        var file = info.OpenAsFile(
            FileMode.Open,
            (backup ? FileSystemFileAccess.Delete : FileSystemFileAccess.WriteAttributes) | (overwriteReadOnly ? default : FileSystemFileAccess.WriteData),
            backup ? FileShare.Delete : FileShare.None);

        try
        {
            file.ThrowIfMetadataMismatch(info);

            // We preserve original file attributes but also apply requested ones
            var newAttributes = file.Attributes | info.Attributes;

            var newInfo = info.Copy()
                .WithSize(size)
                .WithLastWriteTimeUtc(lastWriteTime)
                .WithAttributes(newAttributes);

            var placeholderState = file.GetPlaceholderState().ThrowIfInvalid();

            // Partial placeholder file cannot be backed up
            backup &= !placeholderState.HasFlag(PlaceholderState.Partial);

            // We fully hydrate files that are pinned or available offline
            // (including classic files, that are not yet converted into placeholders).
            var isHydrationRequired = !file.Attributes.IsDehydrationRequested() && (file.Attributes.IsPinned() || !placeholderState.HasFlag(PlaceholderState.Partial));

            IDestinationRevision<long> revisionCreationProcess;

            if (isHydrationRequired)
            {
                var tempInfo = info.Copy().WithAttributes(newAttributes).ToTempFileInfo(tempFileName);
                var tempFile = tempInfo.CreateTemporaryFile(file);
                var checksumVerificationEnabled = await _featureFlagProvider.DownloadChecksumVerificationIsEnabledAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    var fileInfo = tempFile.ToNodeInfo(parentId: 0, refresh: false).WithSize(0).WithPath(tempFile.FullPath);

                    revisionCreationProcess = new ImmediatelyHydratingOnDemandRevisionCreationProcess(
                        tempFile,
                        info,
                        fileInfo,
                        fileInfo.Copy().WithName(info.Name).WithPath(info.Path).WithAttributes(newAttributes).WithLastWriteTimeUtc(lastWriteTime),
                        checksumVerificationEnabled,
                        progressCallback,
                        _recordMetric,
                        _loggerFactory.CreateLogger<ImmediatelyHydratingOnDemandRevisionCreationProcess>());

                    file.Dispose();

                    return revisionCreationProcess;
                }
                catch
                {
                    tempFile.TryDelete();
                    tempFile.Dispose();
                    throw;
                }
            }

            revisionCreationProcess = backup
                ? new BackingUpOnDemandRevisionCreationProcess(newInfo, file)
                : new OnDemandRevisionCreationProcess(newInfo, file);

            return revisionCreationProcess;
        }
        catch
        {
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

        using var fsObject = info.Open(FileSystemFileAccess.ReadAttributes | FileSystemFileAccess.Delete, FileShare.Read | FileShare.Delete);

        fsObject.ThrowIfMetadataMismatch(info);

        var isRename = string.IsNullOrEmpty(destinationInfo.Path);
        if (isRename)
        {
            fsObject.Rename(newName, includeObjectId: true);
        }
        else
        {
            using var newParent = destinationInfo.OpenParentDirectory(
                FileSystemFileAccess.TraverseDirectory | FileSystemFileAccess.ReadAttributes,
                FileShare.ReadWrite);

            fsObject.Move(newParent, newName, includeObjectId: true);
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
        info.SetInSync(out var placeholderState, out var attributes);

        if (attributes.IsExcluded())
        {
            // The file or folder is excluded from sync
            return;
        }

        if (attributes.HasFlag(FileAttributes.Directory))
        {
            info.NotifyChanges();

            // Folders do not require hydration or dehydration
            return;
        }

        if (attributes.IsDehydrationRequested() && !placeholderState.HasFlag(PlaceholderState.PartiallyOnDisk))
        {
            info.Dehydrate();
        }

        info.NotifyChanges();
    }

    public Task HydrateFileAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var file = info.OpenAsFile(FileSystemFileAccess.ReadAttributes, FileShare.ReadWrite | FileShare.Delete);

        file.ThrowIfMetadataMismatch(info);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // ReSharper disable once AccessToDisposedClosure
            using var cancellationRegistration = cancellationToken.Register(() => file.FileHandle.CancelIo());

            CfHydratePlaceholder(file.FileHandle).ThrowExceptionForHR();

            return Task.CompletedTask;
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, file.ObjectId, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }
}
