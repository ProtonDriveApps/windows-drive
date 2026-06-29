using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem;

internal sealed class LoggingFileSystemClientDecorator<TId> : FileSystemClientDecoratorBase<TId>
    where TId : IEquatable<TId>
{
    private readonly ILogger<LoggingFileSystemClientDecorator<TId>> _logger;

    public LoggingFileSystemClientDecorator(
        ILogger<LoggingFileSystemClientDecorator<TId>> logger,
        IFileSystemClient<TId> origin)
        : base(origin)
    {
        _logger = logger;
    }

    public override Task<NodeInfo<TId>> GetInfoAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Getting info about \"{Root}\"/\"{Path}\"/{ParentId}/{Id}",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        return base.GetInfoAsync(info, cancellationToken);
    }

    public override IAsyncEnumerable<NodeInfo<TId>> EnumerateAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Enumerating \"{Root}\"/\"{Path}\"/{ParentId}/{Id}",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        return base.EnumerateAsync(info, cancellationToken);
    }

    public override async Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Opening the file for reading \"{Root}\"/\"{Path}\"/{ParentId}/{Id}",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        var revision = await base.OpenFileForReadingAsync(info, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Opening the file for reading \"{Root}\"/\"{Path}\"/{ParentId}/{Id} completed",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        return revision;
    }

    public override Task<NodeInfo<TId>> CreateDirectoryAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Creating directory \"{Root}\"/\"{Path}\"/{ParentId}/-",
            info.Root?.Id,
            info.Path,
            info.ParentId);

        return base.CreateDirectoryAsync(info, cancellationToken);
    }

    public override async Task<IDestinationRevision<TId>> CreateFileAsync(
        NodeInfo<TId> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Creating file \"{Root}\"/\"{Path}\"/{ParentId}/-, TempFileName=\"{TempFileName}\"",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            tempFileName ?? string.Empty);

        try
        {
            var revisionCreationProcess = await base.CreateFileAsync(
                info,
                tempFileName,
                thumbnailProvider,
                fileMetadataProvider,
                progressCallback,
                cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Creating file \"{Root}\"/\"{Path}\"/{ParentId}/-, TempFileName=\"{TempFileName}\" completed, ready for transferring data",
                info.Root?.Id,
                info.Path,
                info.ParentId,
                tempFileName ?? string.Empty);

            return new LoggingRevisionCreationProcess(_logger, revisionCreationProcess);
        }
        catch (Exception ex) when (IsWorthLoggingStackTrace(ex))
        {
            _logger.LogWarning(ex, "Creating file failed with exception");
            throw;
        }
    }

    public override async Task<IDestinationRevision<TId>> CreateRevisionAsync(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Creating file revision \"{Root}\"/\"{Path}\"/{ParentId}/{Id}, TempFileName=\"{tempFileName}\"",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id,
            tempFileName ?? string.Empty);

        try
        {
            var revisionCreationProcess = await base.CreateRevisionAsync(
                info,
                size,
                lastWriteTime,
                tempFileName,
                thumbnailProvider,
                fileMetadataProvider,
                progressCallback,
                cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Creating file revision \"{Root}\"/\"{Path}\"/{ParentId}/{Id}, TempFileName=\"{tempFileName}\" completed, ready for transferring data",
                info.Root?.Id,
                info.Path,
                info.ParentId,
                info.Id,
                tempFileName ?? string.Empty);

            return new LoggingRevisionCreationProcess(_logger, revisionCreationProcess);
        }
        catch (Exception ex) when (IsWorthLoggingStackTrace(ex))
        {
            _logger.LogWarning(ex, "Creating file revision failed with exception");
            throw;
        }
    }

    public override async Task MoveAsync(NodeInfo<TId> info, NodeInfo<TId> newInfo, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Moving \"{Root}\"/\"{Path}\"/{ParentId}/{Id} to \"{DestPath}\"/{DestParentId}/-",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id,
            !string.IsNullOrEmpty(newInfo.Path) ? newInfo.Path : newInfo.Name,
            newInfo.ParentId);

        try
        {
            await base.MoveAsync(info, newInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsWorthLoggingStackTrace(ex))
        {
            _logger.LogWarning(ex, "Moving failed with exception");
            throw;
        }
    }

    public override async Task DeleteAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Deleting \"{Root}\"/\"{Path}\"/{ParentId}/{Id}",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        try
        {
            await base.DeleteAsync(info, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsWorthLoggingStackTrace(ex))
        {
            _logger.LogWarning(ex, "Deleting failed with exception");
            throw;
        }
    }

    public override async Task DeletePermanentlyAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Permanently deleting \"{Root}\"/\"{Path}\"/{ParentId}/{Id}",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        try
        {
            await base.DeletePermanentlyAsync(info, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsWorthLoggingStackTrace(ex))
        {
            _logger.LogWarning(ex, "Permanently deleting failed with exception");
            throw;
        }
    }

    public override void SetInSyncState(NodeInfo<TId> info)
    {
        _logger.LogDebug(
            "Setting in-sync \"{Root}\"/\"{Path}\"/{ParentId}/{Id}",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        base.SetInSyncState(info);
    }

    public override Task HydrateFileAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Hydrating file \"{Root}\"/\"{Path}\"/{ParentId}/{Id}",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        return base.HydrateFileAsync(info, cancellationToken);
    }

    private static bool IsWorthLoggingStackTrace(Exception ex)
    {
        return ex is not OperationCanceledException &&
            (ex is not FileSystemClientException clientException || clientException.ErrorCode is FileSystemErrorCode.Unknown);
    }

    private sealed class LoggingRevisionCreationProcess : IDestinationRevision<TId>
    {
        private readonly ILogger<LoggingFileSystemClientDecorator<TId>> _logger;
        private readonly IDestinationRevision<TId> _decoratedInstance;

        public LoggingRevisionCreationProcess(ILogger<LoggingFileSystemClientDecorator<TId>> logger, IDestinationRevision<TId> decoratedInstance)
        {
            _logger = logger;
            _decoratedInstance = decoratedInstance;
        }

        public NodeInfo<TId> FileInfo => _decoratedInstance.FileInfo;

        public NodeInfo<TId> BackupInfo
        {
            get => _decoratedInstance.BackupInfo;
            set => _decoratedInstance.BackupInfo = value;
        }

        public bool ImmediateHydrationRequired => _decoratedInstance.ImmediateHydrationRequired;
        public bool ChecksumVerificationEnabled => _decoratedInstance.ChecksumVerificationEnabled;
        public bool CanGetContentStream => _decoratedInstance.CanGetContentStream;

        public Stream GetContentStream()
        {
            try
            {
                return _decoratedInstance.GetContentStream();
            }
            catch (Exception ex) when (IsWorthLoggingStackTrace(ex))
            {
                _logger.LogWarning(ex, "Getting file revision content stream failed with exception");
                throw;
            }
        }

        public async Task WriteContentAsync(Stream source, FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
        {
            try
            {
                await _decoratedInstance.WriteContentAsync(source, expectedChecksum, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsWorthLoggingStackTrace(ex))
            {
                _logger.LogWarning(ex, "Writing file revision content failed with exception");
                throw;
            }
        }

        public async Task<NodeInfo<TId>> FinishAsync(FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
        {
            try
            {
                return await _decoratedInstance.FinishAsync(expectedChecksum, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsWorthLoggingStackTrace(ex))
            {
                _logger.LogWarning(ex, "Finishing file revision creation failed with exception");
                throw;
            }
        }

        public ValueTask DisposeAsync()
        {
            return _decoratedInstance.DisposeAsync();
        }
    }
}
