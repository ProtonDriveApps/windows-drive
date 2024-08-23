using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem;

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

    public override Task<NodeInfo<TId>> GetInfo(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Getting info about \"{Root}\"/\"{Path}\"/{ParentId}/{Id}",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        return base.GetInfo(info, cancellationToken);
    }

    public override IAsyncEnumerable<NodeInfo<TId>> Enumerate(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Enumerating \"{Root}\"/\"{Path}\"/{ParentId}/{Id}",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        return base.Enumerate(info, cancellationToken);
    }

    public override Task<IRevision> OpenFileForReading(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Opening the file for reading \"{Root}\"/\"{Path}\"/{ParentId}/{Id}",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        return base.OpenFileForReading(info, cancellationToken);
    }

    public override Task<NodeInfo<TId>> CreateDirectory(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Creating a directory \"{Root}\"/\"{Path}\"/{ParentId}/-",
            info.Root?.Id,
            info.Path,
            info.ParentId);

        return base.CreateDirectory(info, cancellationToken);
    }

    public override Task<IRevisionCreationProcess<TId>> CreateFile(
        NodeInfo<TId> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Creating a file \"{Root}\"/\"{Path}\"/{ParentId}/-, TempFileName=\"{TempFileName}\"",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            tempFileName ?? string.Empty);

        return base.CreateFile(info, tempFileName, thumbnailProvider, progressCallback, cancellationToken);
    }

    public override Task<IRevisionCreationProcess<TId>> CreateRevision(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Creating a revision \"{Root}\"/\"{Path}\"/{ParentId}/{Id}, TempFileName=\"{tempFileName}\"",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id,
            tempFileName ?? string.Empty);

        return base.CreateRevision(info, size, lastWriteTime, tempFileName, thumbnailProvider, progressCallback, cancellationToken);
    }

    public override Task Move(NodeInfo<TId> info, NodeInfo<TId> newInfo, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Moving \"{Root}\"/\"{Path}\"/{ParentId}/{Id} to \"{DestPath}\"/{DestParentId}/-",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id,
            !string.IsNullOrEmpty(newInfo.Path) ? newInfo.Path : newInfo.Name,
            newInfo.ParentId);

        return base.Move(info, newInfo, cancellationToken);
    }

    public override Task Delete(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Deleting \"{Root}\"/\"{Path}\"/{ParentId}/{Id}",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        return base.Delete(info, cancellationToken);
    }

    public override Task DeletePermanently(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Permanently deleting \"{Root}\"/\"{Path}\"/{ParentId}/{Id}",
            info.Root?.Id,
            info.Path,
            info.ParentId,
            info.Id);

        return base.DeletePermanently(info, cancellationToken);
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
}
