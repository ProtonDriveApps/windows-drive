namespace Proton.Drive.Sdk.Sync.Client.FileUploading;

public interface IPhotoDuplicateService
{
    public Task<(string ContentHash, string Sha1Digest)> GetContentHashAndSha1DigestAsync(
        Stream source,
        string shareId,
        string parentLinkId,
        CancellationToken cancellationToken);

    public Task<ILookup<string, PhotoNameCollision>> GetNameCollisionsAsync(
        string volumeId,
        string shareId,
        string parentLinkId,
        IEnumerable<string> fileNames,
        CancellationToken cancellationToken);
}
