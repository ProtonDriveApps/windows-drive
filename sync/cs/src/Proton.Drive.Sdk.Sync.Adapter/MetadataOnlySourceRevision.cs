using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;

namespace Proton.Drive.Sdk.Sync.Adapter;

/// <summary>
/// File revision that provides only the file metadata known to the adapter, the file content cannot be read.
/// </summary>
internal sealed class MetadataOnlySourceRevision : ISourceRevision
{
    public MetadataOnlySourceRevision(long size, DateTime lastWriteTimeUtc)
    {
        Size = size;
        LastWriteTimeUtc = lastWriteTimeUtc;
    }

    public long Size { get; }
    public bool CanGetContentStream => false;
    public CancellationToken AbortionToken => CancellationToken.None;

    public DateTime CreationTimeUtc => throw new NotSupportedException("Metadata-only file revision does not provide the creation time");
    public DateTime LastWriteTimeUtc { get; }

    public Task<FileContentChecksum> GetContentChecksumAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Metadata-only file revision does not provide the content");
    }

    public Stream GetContentStream()
    {
        throw new NotSupportedException("Metadata-only file revision does not provide the content");
    }

    public Task CheckReadabilityAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CopyContentToAsync(Stream destination, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Metadata-only file revision does not provide the content");
    }

    public bool TryGetFileHasChanged(out bool hasChanged)
    {
        hasChanged = false;
        return false;
    }

    public Task<ReadOnlyMemory<byte>?> TryGetThumbnailAsync(int numberOfPixelsOnLargestSide, int maxNumberOfBytes, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Metadata-only file revision does not provide the thumbnail");
    }

    public Task<FileMetadata?> GetMetadataAsync()
    {
        throw new NotSupportedException("Metadata-only file revision does not provide the file metadata");
    }

    public Task<IReadOnlySet<PhotoTag>> GetPhotoTagsAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Metadata-only file revision does not provide the photo tags");
    }

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
