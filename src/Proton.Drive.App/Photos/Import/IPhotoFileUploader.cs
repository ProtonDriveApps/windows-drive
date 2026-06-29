using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.App.Photos.Import;

internal interface IPhotoFileUploader
{
    Task<NodeInfo<string>> UploadFileAsync(string filePath, string parentLinkId, string? mainPhotoLinkId, CancellationToken cancellationToken);
}
