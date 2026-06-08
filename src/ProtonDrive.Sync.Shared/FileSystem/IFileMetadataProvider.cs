using ProtonDrive.Sync.Shared.FileSystem.Photos;

namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IFileMetadataProvider
{
    DateTime CreationTimeUtc { get; }
    DateTime LastWriteTimeUtc { get; }

    Task<FileMetadata?> GetMetadataAsync();

    Task<IReadOnlySet<PhotoTag>> GetPhotoTagsAsync(CancellationToken cancellationToken);
}
