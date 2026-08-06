using System.Collections.Concurrent;
using System.Security.Cryptography;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Client.Photos.Contracts;
using Proton.Drive.Shared.Devices;

namespace Proton.Drive.Sdk.Sync.Client.FileUploading;

internal sealed class PhotoDuplicateService : IPhotoDuplicateService
{
    private readonly IPhotoHashProvider _photoHashProvider;
    private readonly IPhotoApiClient _photoApiClient;
    private readonly IClientInstanceIdentityProvider _clientInstanceIdentityProvider;

    public PhotoDuplicateService(
        IPhotoHashProvider photoHashProvider,
        IPhotoApiClient photoApiClient,
        IClientInstanceIdentityProvider clientInstanceIdentityProvider)
    {
        _photoHashProvider = photoHashProvider;
        _photoApiClient = photoApiClient;
        _clientInstanceIdentityProvider = clientInstanceIdentityProvider;
    }

    public async Task<(string ContentHash, string Sha1Digest)> GetContentHashAndSha1DigestAsync(
        Stream source,
        string shareId,
        string parentLinkId,
        CancellationToken cancellationToken)
    {
        var hash = await SHA1.HashDataAsync(source, cancellationToken).ConfigureAwait(false);
        var sha1Digest = Convert.ToHexStringLower(hash);
        var contentHash = await _photoHashProvider.GetContentHashAsync(shareId, parentLinkId, sha1Digest, cancellationToken).ConfigureAwait(false);
        return (contentHash, sha1Digest);
    }

    public async Task<ILookup<string, PhotoNameCollision>> GetNameCollisionsAsync(
        string volumeId,
        string shareId,
        string parentLinkId,
        IEnumerable<string> fileNames,
        CancellationToken cancellationToken)
    {
        ConcurrentDictionary<string, string> nameHashesByHash = [];

        await Parallel.ForEachAsync(
                fileNames,
                new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount) },
                async (fileName, ct) =>
                {
                    var nameHash = await _photoHashProvider.GetNameHashAsync(shareId, parentLinkId, fileName, ct).ConfigureAwait(false);
                    nameHashesByHash.TryAdd(nameHash, fileName);
                })
            .ConfigureAwait(false);

        var nameHashes = nameHashesByHash.Keys.ToList();

        var duplicatesResponse = await _photoApiClient.GetDuplicatesAsync(
            volumeId,
            new PhotoDuplicationParameters { NameHashes = nameHashes },
            cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        var clientId = _clientInstanceIdentityProvider.GetClientInstanceId();

        var duplicatesByFileName = new List<PhotoNameCollision>(duplicatesResponse.PhotoDuplicates.Count);

        foreach (var duplicateHash in duplicatesResponse.PhotoDuplicates)
        {
            if (string.IsNullOrEmpty(duplicateHash.LinkId)
                || duplicateHash.LinkState is not LinkState.Draft and not LinkState.Active
                || duplicateHash.NameHash is null)
            {
                // Trashed and deleted remote photos are not considered as duplicates
                continue;
            }

            var isDraftCreatedByThisClientInstance = duplicateHash.LinkState is LinkState.Draft && string.Equals(clientId, duplicateHash.ClientId);

            if (isDraftCreatedByThisClientInstance || !nameHashesByHash.TryGetValue(duplicateHash.NameHash, out var fileName))
            {
                continue;
            }

            var duplicate = new PhotoNameCollision(duplicateHash.LinkId, fileName, duplicateHash.NameHash, duplicateHash.ContentHash);
            duplicatesByFileName.Add(duplicate);
        }

        // Multiple files can exist with the same name
        return duplicatesByFileName.ToLookup(x => x.FileName);
    }
}
