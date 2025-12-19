using Proton.Drive.Sdk.Nodes.Download;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client;

internal sealed class SdkRemoteFileRevision : IRevision
{
    private static readonly Action<long, long> NullProgressCallback = (_, _) => { };

    private readonly FileDownloader _fileDownloader;
    private readonly ExtendedAttributes? _extendedAttributes;
    private readonly Action<Exception> _reportIntegrityFailure;

    public SdkRemoteFileRevision(
        FileDownloader fileDownloader,
        DateTime creationTimeUtc,
        DateTime lastWriteTimeUtc,
        ExtendedAttributes? extendedAttributes,
        long sizeOnStorage,
        Action<Exception> reportIntegrityFailure)
    {
        _fileDownloader = fileDownloader;
        CreationTimeUtc = creationTimeUtc;
        LastWriteTimeUtc = lastWriteTimeUtc;
        _extendedAttributes = extendedAttributes;
        _reportIntegrityFailure = reportIntegrityFailure;

        Size = _extendedAttributes?.Common?.Size ?? sizeOnStorage;
    }

    public long Size { get; }
    public DateTime CreationTimeUtc { get; }
    public DateTime LastWriteTimeUtc { get; }
    public bool CanGetContentStream => false;

    public Stream GetContentStream()
    {
        throw new NotSupportedException("SDK-based remote file revision does not provide a stream");
    }

    public Task CheckReadabilityAsync(CancellationToken cancellationToken)
    {
        // Assume the remote file is always readable
        return Task.CompletedTask;
    }

    public async Task CopyContentToAsync(Stream destination, CancellationToken cancellationToken)
    {
        try
        {
            var controller = _fileDownloader.DownloadToStream(destination, NullProgressCallback, cancellationToken);

            await controller.Completion.ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapSdkClientException(ex, id: null, includeObjectId: false, out var mappedException))
        {
            if (ExceptionMapping.IsSdkIntegrityFailure(ex))
            {
                _reportIntegrityFailure.Invoke(ex);
            }

            throw mappedException;
        }
    }

    public bool TryGetFileHasChanged(out bool hasChanged)
    {
        hasChanged = false;
        return false;
    }

    public Task<ReadOnlyMemory<byte>?> TryGetThumbnailAsync(int numberOfPixelsOnLargestSide, int maxNumberOfBytes, CancellationToken cancellationToken)
    {
        return Task.FromResult<ReadOnlyMemory<byte>?>(null);
    }

    public Task<FileMetadata?> GetMetadataAsync()
    {
        if (_extendedAttributes is null)
        {
            return Task.FromResult(default(FileMetadata?));
        }

        var metadata = FileMetadataSanitizer.GetFileMetadata(
            _extendedAttributes.Media?.Width,
            _extendedAttributes.Media?.Height,
            _extendedAttributes.Media?.Duration,
            _extendedAttributes.Camera?.Orientation,
            _extendedAttributes.Camera?.Device,
            _extendedAttributes.Camera?.CaptureTime,
            _extendedAttributes.Location?.Latitude,
            _extendedAttributes.Location?.Longitude);

        return Task.FromResult(metadata);
    }

    public Task<IReadOnlySet<PhotoTag>> GetPhotoTagsAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public void Dispose()
    {
        _fileDownloader.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
