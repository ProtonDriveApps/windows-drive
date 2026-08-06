using Proton.Drive.Sdk.Sync.Adapter;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;
using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local;

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

        private Stream? _decoratedStream;

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
        public CancellationToken AbortionToken { get; }
        public DateTime CreationTimeUtc => _decoratedInstance.CreationTimeUtc;
        public DateTime LastWriteTimeUtc => _decoratedInstance.LastWriteTimeUtc;

        private LooseCompoundAltIdentity<TAltId> Id { get; }
        private IFileTransferAbortionStrategy<TAltId> AbortionStrategy { get; }

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

            _decoratedStream?.Dispose();
            _decoratedInstance.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            AbortionStrategy.HandleFileClosed(Id);

            if (_decoratedStream is not null)
            {
                await _decoratedStream.DisposeAsync().ConfigureAwait(false);
            }

            await _decoratedInstance.DisposeAsync().ConfigureAwait(false);
        }

        public Task CheckReadabilityAsync(CancellationToken cancellationToken)
        {
            return _decoratedInstance.CheckReadabilityAsync(cancellationToken);
        }

        public Task<FileContentChecksum> GetContentChecksumAsync(CancellationToken cancellationToken) => _decoratedInstance.GetContentChecksumAsync(cancellationToken);

        public Stream GetContentStream()
        {
            // GetContentStream is called when downloading, because local revisions support obtaining content stream, but remote ones don't.
            // Abortion due to local file content change is relevant for uploading only.
            return _decoratedStream ??= new AbortionCapableStream(_decoratedInstance.GetContentStream(), this);
        }

        public bool TryGetFileHasChanged(out bool hasChanged)
        {
            return _decoratedInstance.TryGetFileHasChanged(out hasChanged);
        }

        public Task CopyContentToAsync(Stream destination, CancellationToken cancellationToken)
        {
            return _decoratedInstance.CopyContentToAsync(destination, cancellationToken);
        }

        private sealed class AbortionCapableStream(Stream inner, AbortionCapableRevisionDecorator owner) : WrappingStream(inner, ownsInnerStream: false)
        {
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override int Read(Span<byte> buffer) => throw new NotSupportedException();
            public override int ReadByte() => throw new NotSupportedException();
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => throw new NotSupportedException();
            public override int EndRead(IAsyncResult asyncResult) => throw new NotSupportedException();

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return HandleFileTransferCompletion(
                    await base.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false),
                    count,
                    cancellationToken);
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return HandleFileTransferCompletion(
                    await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false),
                    buffer.Length,
                    cancellationToken);
            }

            public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                await BaseCopyToAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);
            }

            private int HandleFileTransferCompletion(int numberOfBytesRead, int maxNumberOfBytesToRead, CancellationToken cancellationToken)
            {
                if (numberOfBytesRead != 0 || maxNumberOfBytesToRead == 0)
                {
                    return numberOfBytesRead;
                }

                if (owner.TryGetFileHasChanged(out var fileHasChanged) && fileHasChanged)
                {
                    owner.AbortionStrategy.HandleFileChanged(owner.Id);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                return numberOfBytesRead;
            }
        }
    }
}
