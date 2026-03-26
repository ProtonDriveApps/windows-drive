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

    public override async Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<TAltId> info, CancellationToken cancellationToken)
    {
        if (info.Root is null)
        {
            return await base.OpenFileForReadingAsync(info, cancellationToken).ConfigureAwait(false);
        }

        var id = (LooseCompoundAltIdentity<TAltId>)(info.Root.VolumeId, info.Id);
        var abortionToken = _abortionStrategy.HandleFileOpenedForReading(id);

        try
        {
            var revisionToDecorate = await base.OpenFileForReadingAsync(info, cancellationToken).ConfigureAwait(false);

            return new AbortionCapableRevisionDecorator(revisionToDecorate, id, _abortionStrategy, abortionToken);
        }
        catch
        {
            _abortionStrategy.HandleFileClosed(id);
            throw;
        }
    }

    private sealed class AbortionCapableRevisionDecorator : ISourceRevision
    {
        private readonly ISourceRevision _decoratedInstance;

        public AbortionCapableRevisionDecorator(
            ISourceRevision instanceToDecorate,
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
        public bool CanGetContentStream => _decoratedInstance.CanGetContentStream;
        public DateTime CreationTimeUtc => _decoratedInstance.CreationTimeUtc;
        public DateTime LastWriteTimeUtc => _decoratedInstance.LastWriteTimeUtc;

        private LooseCompoundAltIdentity<TAltId> Id { get; }
        private IFileTransferAbortionStrategy<TAltId> AbortionStrategy { get; }
        private CancellationToken AbortionToken { get; }

        public Task<ReadOnlyMemory<byte>?> TryGetThumbnailAsync(int numberOfPixelsOnLargestSide, int maxNumberOfBytes, CancellationToken cancellationToken)
        {
            return _decoratedInstance.TryGetThumbnailAsync(numberOfPixelsOnLargestSide, maxNumberOfBytes, cancellationToken);
        }

        public Task<FileMetadata?> GetMetadataAsync()
        {
            return _decoratedInstance.GetMetadataAsync();
        }

        public Task<IReadOnlySet<PhotoTag>> GetPhotoTagsAsync(CancellationToken cancellationToken)
        {
            return _decoratedInstance.GetPhotoTagsAsync(cancellationToken);
        }

        public void Dispose()
        {
            AbortionStrategy.HandleFileClosed(Id);

            _decoratedInstance.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            AbortionStrategy.HandleFileClosed(Id);

            return _decoratedInstance.DisposeAsync();
        }

        public Task CheckReadabilityAsync(CancellationToken cancellationToken)
        {
            return _decoratedInstance.CheckReadabilityAsync(cancellationToken);
        }

        public Task<ReadOnlyMemory<byte>?> GetSha1Async(CancellationToken cancellationToken) => _decoratedInstance.GetSha1Async(cancellationToken);

        public Stream GetContentStream()
        {
            // GetContentStream is called when downloading, because local revisions support obtaining content stream, but remote ones don't.
            // Abortion due to local file content change is relevant for uploading only.
            return new AbortionCapableStream(_decoratedInstance.GetContentStream(), this);
        }

        public bool TryGetFileHasChanged(out bool hasChanged)
        {
            return _decoratedInstance.TryGetFileHasChanged(out hasChanged);
        }

        public Task CopyContentToAsync(Stream destination, CancellationToken cancellationToken)
        {
            return _decoratedInstance.CopyContentToAsync(destination, cancellationToken);
        }

        private sealed class AbortionCapableStream(Stream inner, AbortionCapableRevisionDecorator owner) : WrappingStream(inner)
        {
            public override long Length
            {
                get
                {
                    var length = base.Length;

                    if (length != owner.Size)
                    {
                        ThrowFileHasChanged();
                    }

                    return length;
                }
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                ThrowIfAbortionRequested();

                return HandleFileTransferCompletion(
                    await base.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false),
                    count);
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                ThrowIfAbortionRequested();

                return HandleFileTransferCompletion(
                    await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false),
                    buffer.Length);
            }

            public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, owner.AbortionToken);

                try
                {
                    await base.CopyToAsync(destination, bufferSize, linkedToken.Token).ConfigureAwait(false);

                    ThrowIfFileHasChanged();
                }
                catch (Exception exception) when (exception is OperationCanceledException)
                {
                    ThrowIfAbortionRequested();
                    throw;
                }
            }

            private int HandleFileTransferCompletion(int numberOfBytesRead, int maxNumberOfBytesToRead)
            {
                ThrowIfAbortionRequested();

                if (numberOfBytesRead == 0 && maxNumberOfBytesToRead != 0)
                {
                    ThrowIfFileHasChanged();
                }

                return numberOfBytesRead;
            }

            private void ThrowIfFileHasChanged()
            {
                if (!owner.TryGetFileHasChanged(out var fileHasChanged) || !fileHasChanged)
                {
                    return;
                }

                ThrowFileHasChanged();
            }

            private void ThrowFileHasChanged()
            {
                owner.AbortionStrategy.HandleFileChanged(owner.Id);

                ThrowIfAbortionRequested();
            }

            private void ThrowIfAbortionRequested()
            {
                if (owner.AbortionToken.IsCancellationRequested)
                {
                    throw new FileSystemClientException(
                        "File transfer aborted. File has changed before the transfer was completed",
                        FileSystemErrorCode.TransferAbortedDueToFileChange);
                }
            }
        }
    }
}
