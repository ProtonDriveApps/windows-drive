using System.Security.Cryptography;
using Proton.Drive.Sdk.Sync.Client.Cryptography;
using Proton.Drive.Sdk.Sync.Client.RemoteNodes;

namespace Proton.Drive.Sdk.Sync.Client.FileUploading;

internal sealed class PhotoHashProvider : IPhotoHashProvider
{
    private readonly IRemoteNodeService _remoteNodeService;
    private readonly ICryptographyService _cryptographyService;

    public PhotoHashProvider(IRemoteNodeService remoteNodeService, ICryptographyService cryptographyService)
    {
        _remoteNodeService = remoteNodeService;
        _cryptographyService = cryptographyService;
    }

    public async Task<string> GetContentHashAsync(string shareId, string parentLinkId, string sha1Digest, CancellationToken cancellationToken)
    {
        var node = await _remoteNodeService.GetRemoteNodeAsync(shareId, parentLinkId, cancellationToken).ConfigureAwait(false);

        if (node is not RemoteFolder nodeFolder)
        {
            throw new CryptographicException("Cannot compute content hash: hash key not found");
        }

        return _cryptographyService.HashContentDigestHex(nodeFolder.HashKey, sha1Digest);
    }

    public async Task<string> GetNameHashAsync(string shareId, string parentLinkId, string filename, CancellationToken cancellationToken)
    {
        var node = await _remoteNodeService.GetRemoteNodeAsync(shareId, parentLinkId, cancellationToken).ConfigureAwait(false);

        if (node is not RemoteFolder nodeFolder)
        {
            throw new CryptographicException("Cannot compute content hash: hash key not found");
        }

        return _cryptographyService.HashNodeNameHex(nodeFolder.HashKey, filename);
    }
}
