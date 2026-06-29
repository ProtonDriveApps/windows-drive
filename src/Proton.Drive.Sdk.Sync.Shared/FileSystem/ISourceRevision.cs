namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public interface ISourceRevision : IThumbnailProvider, IFileMetadataProvider, IDisposable, IAsyncDisposable
{
    long Size { get; }
    bool CanGetContentStream { get; }
    CancellationToken AbortionToken { get; }

    Task<FileContentChecksum> GetContentChecksumAsync(CancellationToken cancellationToken);
    Stream GetContentStream();
    Task CheckReadabilityAsync(CancellationToken cancellationToken);
    Task CopyContentToAsync(Stream destination, CancellationToken cancellationToken);
    bool TryGetFileHasChanged(out bool hasChanged);
}
