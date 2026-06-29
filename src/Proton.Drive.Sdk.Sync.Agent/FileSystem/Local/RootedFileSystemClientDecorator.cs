using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local;

internal class RootedFileSystemClientDecorator : FileSystemClientDecoratorBase<long>
{
    private readonly IRootDirectory<long> _rootDirectory;

    public RootedFileSystemClientDecorator(IRootDirectory<long> rootDirectory, IFileSystemClient<long> origin)
        : base(origin)
    {
        _rootDirectory = rootDirectory;

        if (IsDefault(_rootDirectory.Id))
        {
            throw new ArgumentException("Root folder identity value must be specified", nameof(rootDirectory));
        }
    }

    public override void Connect(string syncRootPath, IFileHydrationDemandHandler<long> fileHydrationDemandHandler)
    {
        var path = Path.Combine(_rootDirectory.Path, syncRootPath);

        base.Connect(path, fileHydrationDemandHandler);
    }

    public override async Task<NodeInfo<long>> GetInfoAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        if (!IsRoot(info))
        {
            return ToRelative(await base.GetInfoAsync(ToAbsolute(info), cancellationToken).ConfigureAwait(false));
        }

        if (!string.IsNullOrEmpty(info.Path))
        {
            throw new ArgumentException("The root folder path must be empty", nameof(info));
        }

        // The request about the root node always succeeds, the response is crafted from known data.
        return NodeInfo<long>.Directory()
            .WithId(_rootDirectory.Id)
            .WithName(string.Empty);
    }

    public override IAsyncEnumerable<NodeInfo<long>> EnumerateAsync(NodeInfo<long> info, CancellationToken cancellationToken)
        => base.EnumerateAsync(ToAbsolute(info), cancellationToken).Select(ToRelative);

    public override Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<long> info, CancellationToken cancellationToken)
        => base.OpenFileForReadingAsync(ToAbsolute(info), cancellationToken);

    public override Task<NodeInfo<long>> CreateDirectoryAsync(NodeInfo<long> info, CancellationToken cancellationToken)
        => base.CreateDirectoryAsync(ToAbsolute(info), cancellationToken);

    public override Task<IDestinationRevision<long>> CreateFileAsync(
        NodeInfo<long> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
        => base.CreateFileAsync(ToAbsolute(info), tempFileName, thumbnailProvider, fileMetadataProvider, progressCallback, cancellationToken);

    public override async Task<IDestinationRevision<long>> CreateRevisionAsync(
        NodeInfo<long> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
        => new RootedFileWriteProcess(
            await base.CreateRevisionAsync(
                    ToAbsolute(info),
                    size,
                    lastWriteTime,
                    tempFileName,
                    thumbnailProvider,
                    fileMetadataProvider,
                    progressCallback,
                    cancellationToken)
                .ConfigureAwait(false),
            this);

    public override Task MoveAsync(NodeInfo<long> info, NodeInfo<long> newInfo, CancellationToken cancellationToken)
    {
        var nodeInfo = ToAbsolute(info);

        return base.MoveAsync(nodeInfo, ToAbsoluteDestination(nodeInfo, newInfo), cancellationToken);
    }

    public override Task DeleteAsync(NodeInfo<long> info, CancellationToken cancellationToken)
        => base.DeleteAsync(ToAbsolute(info), cancellationToken);

    public override Task DeletePermanentlyAsync(NodeInfo<long> info, CancellationToken cancellationToken)
        => base.DeletePermanentlyAsync(ToAbsolute(info), cancellationToken);

    public override Task DeleteRevisionAsync(NodeInfo<long> info, CancellationToken cancellationToken)
        => base.DeleteRevisionAsync(ToAbsolute(info), cancellationToken);

    public override void SetInSyncState(NodeInfo<long> info)
        => base.SetInSyncState(ToAbsolute(info));

    public override Task HydrateFileAsync(NodeInfo<long> info, CancellationToken cancellationToken)
        => base.HydrateFileAsync(ToAbsolute(info), cancellationToken);

    private static bool IsDefault(long value)
    {
        return value.Equals(0);
    }

    private bool IsRoot(NodeInfo<long> info)
    {
        return (IsDefault(info.Id) && string.IsNullOrEmpty(info.Path)) ||
               (!IsDefault(info.Id) && info.Id.Equals(_rootDirectory.Id));
    }

    private NodeInfo<long> ToAbsolute(NodeInfo<long> nodeInfo)
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

    private NodeInfo<long> ToAbsoluteDestination(NodeInfo<long> nodeInfo, NodeInfo<long> destinationInfo)
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

    private NodeInfo<long> ToRelative(NodeInfo<long> nodeInfo)
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

    private class RootedFileWriteProcess : IDestinationRevision<long>
    {
        private readonly IDestinationRevision<long> _decoratedInstance;
        private readonly RootedFileSystemClientDecorator _converter;

        public RootedFileWriteProcess(IDestinationRevision<long> instanceToDecorate, RootedFileSystemClientDecorator converter)
        {
            _decoratedInstance = instanceToDecorate;
            _converter = converter;
        }

        public NodeInfo<long> FileInfo => _decoratedInstance.FileInfo;

        public NodeInfo<long> BackupInfo
        {
            get => _converter.ToRelative(_decoratedInstance.BackupInfo);
            set => _decoratedInstance.BackupInfo = _converter.ToAbsolute(value);
        }

        public bool ImmediateHydrationRequired => _decoratedInstance.ImmediateHydrationRequired;
        public bool ChecksumVerificationEnabled => _decoratedInstance.ChecksumVerificationEnabled;
        public bool CanGetContentStream => _decoratedInstance.CanGetContentStream;

        public Stream GetContentStream()
        {
            return _decoratedInstance.GetContentStream();
        }

        public Task WriteContentAsync(Stream source, FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
        {
            return _decoratedInstance.WriteContentAsync(source, expectedChecksum, cancellationToken);
        }

        public Task<NodeInfo<long>> FinishAsync(FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
        {
            return _decoratedInstance.FinishAsync(expectedChecksum, cancellationToken);
        }

        public ValueTask DisposeAsync() => _decoratedInstance.DisposeAsync();
    }
}
