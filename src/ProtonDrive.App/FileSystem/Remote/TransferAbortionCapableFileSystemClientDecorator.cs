using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Adapter;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.App.FileSystem.Remote;

internal sealed class TransferAbortionCapableFileSystemClientDecorator<TAltId> : FileSystemClientDecoratorBase<TAltId>
    where TAltId : IEquatable<TAltId>
{
    private readonly IFileTransferAbortionStrategy<TAltId> _abortionStrategy;

    public TransferAbortionCapableFileSystemClientDecorator(
        IFileTransferAbortionStrategy<TAltId> abortionStrategy,
        IFileSystemClient<TAltId> instanceToDecorate)
        : base(instanceToDecorate)
    {
        _abortionStrategy = abortionStrategy;
    }

    public override async Task<IRevision> OpenFileForReading(NodeInfo<TAltId> info, CancellationToken cancellationToken)
    {
        if (info.Root is null)
        {
            return await base.OpenFileForReading(info, cancellationToken).ConfigureAwait(false);
        }

        var id = (LooseCompoundAltIdentity<TAltId>)(info.Root.VolumeId, info.Id);
        var abortionToken = _abortionStrategy.HandleFileOpenedForReading(id);

        try
        {
            var revisionToDecorate = await base.OpenFileForReading(info, cancellationToken).ConfigureAwait(false);

            return new AbortionCapableRevisionDecorator(revisionToDecorate, id, _abortionStrategy, abortionToken);
        }
        catch
        {
            _abortionStrategy.HandleFileClosed(id);
            throw;
        }
    }

    private sealed class AbortionCapableRevisionDecorator : IRevision
    {
        private readonly IRevision _decoratedInstance;

        public AbortionCapableRevisionDecorator(
            IRevision instanceToDecorate,
            LooseCompoundAltIdentity<TAltId> id,
            IFileTransferAbortionStrategy<TAltId> abortionStrategy,
            CancellationToken abortionToken)
        {
            _decoratedInstance = instanceToDecorate;
            Id = id;
            AbortionStrategy = abortionStrategy;
            AbortionToken = abortionToken;
        }

        public long Size => _decoratedInstance.Size;

        public DateTime LastWriteTimeUtc => _decoratedInstance.LastWriteTimeUtc;

        private LooseCompoundAltIdentity<TAltId> Id { get; }
        private IFileTransferAbortionStrategy<TAltId> AbortionStrategy { get; }
        private CancellationToken AbortionToken { get; }

        public bool TryGetThumbnail(int numberOfPixelsOnLargestSide, int maxNumberOfBytes, out ReadOnlyMemory<byte> thumbnailBytes)
        {
            return _decoratedInstance.TryGetThumbnail(numberOfPixelsOnLargestSide, maxNumberOfBytes, out thumbnailBytes);
        }

        public void Dispose()
        {
            _decoratedInstance.Dispose();

            AbortionStrategy.HandleFileClosed(Id);
        }

        public ValueTask DisposeAsync()
        {
            return _decoratedInstance.DisposeAsync();
        }

        public Task CheckReadabilityAsync(CancellationToken cancellationToken)
        {
            return _decoratedInstance.CheckReadabilityAsync(cancellationToken);
        }

        public Stream GetContentStream()
        {
            return new AbortionCapableStream(_decoratedInstance.GetContentStream(), this);
        }

        public bool TryGetFileHasChanged(out bool hasChanged)
        {
            return _decoratedInstance.TryGetFileHasChanged(out hasChanged);
        }

        private sealed class AbortionCapableStream : WrappingStream
        {
            private readonly AbortionCapableRevisionDecorator _owner;

            public AbortionCapableStream(Stream origin, AbortionCapableRevisionDecorator owner)
                : base(origin)
            {
                _owner = owner;
            }

            public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _owner.AbortionToken);

                try
                {
                    await base.CopyToAsync(destination, bufferSize, linkedToken.Token).ConfigureAwait(false);

                    if (_owner.TryGetFileHasChanged(out var fileHasChanged) && fileHasChanged)
                    {
                        _owner.AbortionStrategy.HandleFileChanged(_owner.Id);

                        linkedToken.Token.ThrowIfCancellationRequested();
                    }
                }
                catch (Exception exception) when (exception is TaskCanceledException or OperationCanceledException)
                {
                    if (_owner.AbortionToken.IsCancellationRequested)
                    {
                        throw new FileSystemClientException(
                            "File transfer aborted. File has changed before the transfer was completed",
                            FileSystemErrorCode.TransferAbortedDueToFileChange,
                            exception);
                    }

                    throw;
                }
            }
        }
    }
}
