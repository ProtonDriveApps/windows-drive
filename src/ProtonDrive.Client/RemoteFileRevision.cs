using System.Security.Cryptography;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client;

internal sealed class RemoteFileRevision : ISourceRevision
{
    private readonly Stream _contentStream;
    private readonly ExtendedAttributes? _extendedAttributes;

    public RemoteFileRevision(Stream contentStream, DateTime creationTimeUtc, DateTime lastWriteTimeUtc, ExtendedAttributes? extendedAttributes)
    {
        _contentStream = contentStream;
        _extendedAttributes = extendedAttributes;
        CreationTimeUtc = creationTimeUtc;
        LastWriteTimeUtc = lastWriteTimeUtc;
    }

    public long Size => _contentStream.Length;
    public bool CanGetContentStream => false;
    public CancellationToken AbortionToken { get; } = CancellationToken.None;

    public DateTime CreationTimeUtc { get; }
    public DateTime LastWriteTimeUtc { get; }

    public Task CheckReadabilityAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<ReadOnlyMemory<byte>?> GetSha1Async(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(GetSha1());

        ReadOnlyMemory<byte>? GetSha1()
        {
            var hexSha1 = _extendedAttributes?.Common?.Digests?.Sha1;

            if (string.IsNullOrEmpty(hexSha1))
            {
                return null;
            }

            try
            {
                var sha1 = Convert.FromHexString(hexSha1);

                return sha1.Length == SHA1.HashSizeInBytes ? sha1 : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }

    public Stream GetContentStream()
    {
        throw new NotSupportedException();
    }

    public Task CopyContentToAsync(Stream destination, CancellationToken cancellationToken)
    {
        return _contentStream.CopyToAsync(destination, cancellationToken);
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
        _contentStream.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _contentStream.DisposeAsync();
    }
}
