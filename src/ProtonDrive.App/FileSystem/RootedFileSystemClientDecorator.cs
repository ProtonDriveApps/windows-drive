using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem;

internal sealed class RootedFileSystemClientDecorator<TId> : FileSystemClientDecoratorBase<TId>
    where TId : IEquatable<TId>
{
    private readonly IRootDirectory<TId> _rootDirectory;
    private readonly string? _rootFileName;

    public RootedFileSystemClientDecorator(IRootDirectory<TId> rootDirectory, string? rootFileName, IFileSystemClient<TId> origin)
        : base(origin)
    {
        _rootDirectory = rootDirectory;
        _rootFileName = rootFileName;

        if (IsDefault(_rootDirectory.Id))
        {
            throw new ArgumentException("Root folder identity value must be specified", nameof(rootDirectory));
        }
    }

    public override void Connect(string syncRootPath, IFileHydrationDemandHandler<TId> fileHydrationDemandHandler)
    {
        var path = _rootFileName is not null ? Path.Combine(_rootDirectory.Path, _rootFileName) : _rootDirectory.Path;
        base.Connect(path, fileHydrationDemandHandler);
    }

    public override async Task<NodeInfo<TId>> GetInfo(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        if (!IsRoot(info))
        {
            return ToRelative(await base.GetInfo(ToAbsolute(info), cancellationToken).ConfigureAwait(false));
        }

        if (!string.IsNullOrEmpty(info.Path))
        {
            throw new ArgumentException($"The root folder {nameof(info.Path)} must be empty", nameof(info));
        }

        // The request about the root node always succeeds, the response is crafted from known data.
        return NodeInfo<TId>.Directory()
            .WithId(_rootDirectory.Id)
            .WithName(string.Empty);
    }

    public override IAsyncEnumerable<NodeInfo<TId>> Enumerate(NodeInfo<TId> info, CancellationToken cancellationToken)
        => base.Enumerate(ToAbsolute(info), cancellationToken).Select(ToRelative);

    public override Task<IRevision> OpenFileForReading(NodeInfo<TId> info, CancellationToken cancellationToken)
        => base.OpenFileForReading(ToAbsolute(info), cancellationToken);

    public override Task<NodeInfo<TId>> CreateDirectory(NodeInfo<TId> info, CancellationToken cancellationToken)
        => base.CreateDirectory(ToAbsolute(info), cancellationToken);

    public override Task<IRevisionCreationProcess<TId>> CreateFile(
        NodeInfo<TId> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
        => base.CreateFile(ToAbsolute(info), tempFileName, thumbnailProvider, progressCallback, cancellationToken);

    public override async Task<IRevisionCreationProcess<TId>> CreateRevision(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
        => new RootedFileWriteProcess(
            await base.CreateRevision(ToAbsolute(info), size, lastWriteTime, tempFileName, thumbnailProvider, progressCallback, cancellationToken)
                .ConfigureAwait(false),
            this);

    public override Task Move(NodeInfo<TId> info, NodeInfo<TId> newInfo, CancellationToken cancellationToken)
    {
        var nodeInfo = ToAbsolute(info);

        return base.Move(nodeInfo, ToAbsoluteDestination(nodeInfo, newInfo), cancellationToken);
    }

    public override Task Delete(NodeInfo<TId> info, CancellationToken cancellationToken)
        => base.Delete(ToAbsolute(info), cancellationToken);

    public override Task DeletePermanently(NodeInfo<TId> info, CancellationToken cancellationToken)
        => base.DeletePermanently(ToAbsolute(info), cancellationToken);

    public override Task DeleteRevision(NodeInfo<TId> info, CancellationToken cancellationToken)
        => base.DeleteRevision(ToAbsolute(info), cancellationToken);

    public override void SetInSyncState(NodeInfo<TId> info)
        => base.SetInSyncState(ToAbsolute(info));

    public override Task HydrateFileAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
        => base.HydrateFileAsync(ToAbsolute(info), cancellationToken);

    private static bool IsDefault([NotNullWhen(false)] TId? value)
    {
        return value is null || value.Equals(default);
    }

    private bool IsRoot(NodeInfo<TId> info)
    {
        return (IsDefault(info.Id) && string.IsNullOrEmpty(info.Path)) ||
               (!IsDefault(info.Id) && info.Id.Equals(_rootDirectory.Id));
    }

    private NodeInfo<TId> ToAbsolute(NodeInfo<TId> nodeInfo)
    {
        var info = nodeInfo.Copy().WithPath(ToAbsolutePath(nodeInfo.Path));

        if (IsDefault(info.Id))
        {
            // Empty Path means it's the replica root directory
            if (string.IsNullOrEmpty(info.Path))
            {
                info = info.WithId(_rootDirectory.Id);
            }
        }

        if (IsDefault(info.ParentId) && !string.IsNullOrEmpty(info.Path))
        {
            // Not empty path without directory name means parent is the replica root directory
            if (string.IsNullOrEmpty(Path.GetDirectoryName(info.Path)))
            {
                info = info.WithParentId(_rootDirectory.Id);
            }
        }

        return info;
    }

    private NodeInfo<TId> ToAbsoluteDestination(NodeInfo<TId> nodeInfo, NodeInfo<TId> destinationInfo)
    {
        var info = destinationInfo.Copy().WithPath(ToAbsoluteDestinationPath(destinationInfo.Path));

        // Destination cannot be the replica root directory, only the parent can be.
        if (IsDefault(info.ParentId))
        {
            // The destination is on the same parent as the source
            if (string.IsNullOrEmpty(info.Path))
            {
                info = info.WithParentId(nodeInfo.ParentId);
            }

            // Not empty path without directory name means parent is the replica root directory
            else if (!string.IsNullOrEmpty(info.Path) && string.IsNullOrEmpty(Path.GetDirectoryName(info.Path)))
            {
                info = info.WithParentId(_rootDirectory.Id);
            }
        }

        return info;
    }

    private NodeInfo<TId> ToRelative(NodeInfo<TId> nodeInfo)
    {
        return string.IsNullOrEmpty(nodeInfo.Path)
            ? nodeInfo
            : nodeInfo.Copy().WithPath(ToRelativePath(nodeInfo.Path));
    }

    private string ToAbsoluteDestinationPath(string path)
    {
        return !string.IsNullOrEmpty(path) ? ToAbsolutePath(path) : path;
    }

    private string ToAbsolutePath(string path)
    {
        return Path.Combine(_rootDirectory.Path, path);
    }

    private string ToRelativePath(string path)
    {
        var relativePath = Path.GetRelativePath(_rootDirectory.Path, path);

        return relativePath != path ? relativePath : string.Empty;
    }

    private class RootedFileWriteProcess : IRevisionCreationProcess<TId>
    {
        private readonly IRevisionCreationProcess<TId> _decoratedInstance;
        private readonly RootedFileSystemClientDecorator<TId> _converter;

        public RootedFileWriteProcess(IRevisionCreationProcess<TId> instanceToDecorate, RootedFileSystemClientDecorator<TId> converter)
        {
            _decoratedInstance = instanceToDecorate;
            _converter = converter;
        }

        public NodeInfo<TId> FileInfo => _decoratedInstance.FileInfo;

        public NodeInfo<TId> BackupInfo
        {
            get => _converter.ToRelative(_decoratedInstance.BackupInfo);
            set => _decoratedInstance.BackupInfo = _converter.ToAbsolute(value);
        }

        public bool ImmediateHydrationRequired => _decoratedInstance.ImmediateHydrationRequired;

        public Stream OpenContentStream()
        {
            return _decoratedInstance.OpenContentStream();
        }

        public Task<NodeInfo<TId>> FinishAsync(CancellationToken cancellationToken)
        {
            return _decoratedInstance.FinishAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => _decoratedInstance.DisposeAsync();
    }
}
