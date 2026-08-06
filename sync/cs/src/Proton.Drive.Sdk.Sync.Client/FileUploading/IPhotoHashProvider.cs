namespace Proton.Drive.Sdk.Sync.Client.FileUploading;

public interface IPhotoHashProvider
{
    public Task<string> GetContentHashAsync(string shareId, string parentLinkId, string sha1Digest, CancellationToken cancellationToken);
    public Task<string> GetNameHashAsync(string shareId, string parentLinkId, string filename, CancellationToken cancellationToken);
}
