using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public interface IFileMetadataProvider
{
    DateTime CreationTimeUtc { get; }
    DateTime LastWriteTimeUtc { get; }

    Task<FileMetadata?> GetMetadataAsync();

    Task<IReadOnlySet<PhotoTag>> GetPhotoTagsAsync(CancellationToken cancellationToken);
}
