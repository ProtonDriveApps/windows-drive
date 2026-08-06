namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;

public interface IPhotoTagsGenerator
{
    Task<IReadOnlySet<PhotoTag>> GetPhotoTagsAsync(string filePath, CancellationToken cancellationToken);
}
