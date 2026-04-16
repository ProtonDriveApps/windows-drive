using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using MoreLinq;
using ProtonDrive.Client;
using ProtonDrive.Client.Albums;
using ProtonDrive.Client.Albums.Contracts;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Photos.Albums;

internal sealed class PhotoAlbumDuplicateService : IPhotoAlbumDuplicateService
{
    private readonly IPhotoApiClient _photoApiClient;
    private readonly IAlbumNodeService _albumNodeService;
    private readonly ILogger<PhotoAlbumDuplicateService> _logger;

    private IReadOnlyDictionary<string, string>? _albumsByNameCache;

    public PhotoAlbumDuplicateService(
        IPhotoApiClient photoApiClient,
        IAlbumNodeService albumNodeService,
        ILogger<PhotoAlbumDuplicateService> logger)
    {
        _photoApiClient = photoApiClient;
        _albumNodeService = albumNodeService;
        _logger = logger;
    }

    public async ValueTask<string?> FindDuplicateAlbumIdAsync(string volumeId, string shareId, string albumName, CancellationToken cancellationToken)
    {
        _albumsByNameCache ??= await BuildCacheAsync(volumeId, shareId, cancellationToken).ConfigureAwait(false);

        return _albumsByNameCache.GetValueOrDefault(albumName);
    }

    private static Link GetAlbumLink(LinkDto linkResponse, AlbumDto albumLinkResponse)
    {
        return new Link
        {
            Id = linkResponse.Id,
            ParentId = linkResponse.ParentId,
            Type = linkResponse.Type,
            State = linkResponse.State,
            CreationTime = linkResponse.CreationTime,
            ModificationTime = linkResponse.ModificationTime,
            DeletionTime = linkResponse.DeletionTime,
            Name = linkResponse.Name,
            NameSignatureEmailAddress = linkResponse.NameSignatureEmailAddress,
            NameHash = linkResponse.NameHash,
            NodeKey = linkResponse.NodeKey,
            NodePassphrase = linkResponse.NodePassphrase,
            NodePassphraseSignature = linkResponse.NodePassphraseSignature,
            SignatureEmailAddress = linkResponse.SignatureEmailAddress,
            AlbumProperties = new AlbumProperties
            {
                NodeHashKey = albumLinkResponse.NodeHashKey,
                LastActivityTime = albumLinkResponse.LastActivityTime,
                PhotoCount = albumLinkResponse.PhotoCount,
            },
        };
    }

    private async Task<Dictionary<string, string>> BuildCacheAsync(string volumeId, string shareId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Building album duplicate cache");

        var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await foreach (var albumInfo in EnumerateAlbumsAsync(volumeId, shareId, cancellationToken).ConfigureAwait(false))
        {
            cache.TryAdd(albumInfo.Name, albumInfo.LinkId);
        }

        _logger.LogInformation("Album duplicate cache built: {Count} album(s) indexed", cache.Count);

        return cache;
    }

    private async IAsyncEnumerable<AlbumInfo> EnumerateAlbumsAsync(
        string volumeId,
        string shareId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        const int batchSize = 150;
        string? anchorId = null;
        var hasMoreData = true;

        while (hasMoreData)
        {
            var response = await _photoApiClient.GetAlbumsAsync(volumeId, anchorId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
            hasMoreData = response.HasMoreData;

            _logger.LogDebug("Fetched album list page: {Count} album(s), has more: {HasMoreData}", response.Albums.Count, hasMoreData);

            if (hasMoreData && anchorId == response.AnchorId)
            {
                _logger.LogWarning("Pagination anchor ID did not advance, stopping album list fetch to prevent infinite loop");
                break;
            }

            anchorId = response.AnchorId;

            foreach (var batch in response.Albums.Batch(batchSize))
            {
                var linksResponse = await FetchLinksAsync(volumeId, batch.Select(x => x.LinkId), cancellationToken).ConfigureAwait(false);
                var albumInfos = await DecryptAlbumInfosAsync(shareId, linksResponse.Links, cancellationToken).ConfigureAwait(false);
                foreach (var albumInfo in albumInfos)
                {
                    if (albumInfo.BiometricsRequired)
                    {
                        continue;
                    }

                    yield return albumInfo;
                }
            }
        }
    }

    private async Task<LinkResponseListV2> FetchLinksAsync(string volumeId, IEnumerable<string> linkIds, CancellationToken cancellationToken)
    {
        var parameters = new LinkIdListParameter
        {
            LinkIds = linkIds.ToList(),
        };

        return await _photoApiClient.GetLinksDetailsAsync(volumeId, parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<AlbumInfo>> DecryptAlbumInfosAsync(
        string shareId,
        IReadOnlyList<LinkResponseV2> links,
        CancellationToken cancellationToken)
    {
        var decryptAlbumTasks = links
            .Where(x => x.Album is not null)
            .Select(x => DecryptAlbumInfoAsync(shareId, x, cancellationToken));

        var albums = await Task.WhenAll(decryptAlbumTasks).ConfigureAwait(false);

        return albums.OfType<AlbumInfo>().ToList();
    }

    private async Task<AlbumInfo?> DecryptAlbumInfoAsync(string shareId, LinkResponseV2 linkResponse, CancellationToken cancellationToken)
    {
        if (linkResponse.Album is null)
        {
            return null;
        }

        try
        {
            var albumLink = GetAlbumLink(linkResponse.Link, linkResponse.Album);

            var albumName = await _albumNodeService.GetAlbumNameAsync(shareId, albumLink, cancellationToken).ConfigureAwait(false);

            return new AlbumInfo(
                linkResponse.Link.Id,
                albumName,
                linkResponse.Album.PhotoCount,
                DateTimeOffset.FromUnixTimeSeconds(linkResponse.Album.LastActivityTime).UtcDateTime,
                linkResponse.Album.BiometricsRequired);
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogWarning(ex, "Failed to decrypt album with ID: {LinkId}", linkResponse.Link.Id);
            return null;
        }
    }
}
