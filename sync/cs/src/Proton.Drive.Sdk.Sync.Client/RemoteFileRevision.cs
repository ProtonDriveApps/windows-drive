using System.Security.Cryptography;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;

namespace Proton.Drive.Sdk.Sync.Client;

internal sealed class RemoteFileRevision : ISourceRevision
{
    private readonly Stream _contentStream;
    private readonly ExtendedAttributes? _extendedAttributes;
    private readonly bool? _checksumVerified;

    public RemoteFileRevision(
        Stream contentStream,
        DateTime creationTimeUtc,
        DateTime lastWriteTimeUtc,
        ExtendedAttributes? extendedAttributes,
        bool? checksumVerified)
    {
        _contentStream = contentStream;
        _extendedAttributes = extendedAttributes;
        _checksumVerified = checksumVerified;
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

    public Task<FileContentChecksum> GetContentChecksumAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(GetChecksum());

        FileContentChecksum GetChecksum()
        {
            var hexSha1 = _extendedAttributes?.Common?.Digests?.Sha1;

            if (string.IsNullOrEmpty(hexSha1))
            {
                return FileContentChecksum.Empty;
            }

            try
            {
                var sha1 = Convert.FromHexString(hexSha1);

                return sha1.Length == SHA1.HashSizeInBytes
                    ? new FileContentChecksum { Sha1 = sha1, Sha1Verified = _checksumVerified ?? false }
                    : FileContentChecksum.Empty;
            }
            catch (FormatException)
            {
                return FileContentChecksum.Empty;
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
