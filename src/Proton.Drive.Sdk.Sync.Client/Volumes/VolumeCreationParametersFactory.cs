using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Cryptography;
using Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Shares.Contracts;
using Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.Volumes;

internal class VolumeCreationParametersFactory : IVolumeCreationParametersFactory
{
    private readonly ICryptographyService _cryptographyService;

    public VolumeCreationParametersFactory(ICryptographyService cryptographyService)
    {
        _cryptographyService = cryptographyService;
    }

    public async Task<VolumeCreationParameters> CreateForMainVolumeAsync(CancellationToken cancellationToken)
    {
        var (shareParameters, folderParameters) = await CreateParametersAsync(cancellationToken).ConfigureAwait(false);

        return new VolumeCreationParameters
        {
            // Share creation parameters
            AddressId = shareParameters.AddressId,
            AddressKeyId = shareParameters.AddressKeyId,
            ShareKey = shareParameters.Key,
            SharePassphrase = shareParameters.Passphrase,
            SharePassphraseSignature = shareParameters.PassphraseSignature,

            // Folder creation parameters
            FolderName = folderParameters.Name,
            FolderKey = folderParameters.NodeKey,
            FolderPassphrase = folderParameters.NodePassphrase,
            FolderPassphraseSignature = folderParameters.NodePassphraseSignature,
            FolderHashKey = folderParameters.NodeHashKey,
        };
    }

    public async Task<PhotoVolumeCreationParameters> CreateForPhotoVolumeAsync(CancellationToken cancellationToken)
    {
        var (shareParameters, folderParameters) = await CreateParametersAsync(cancellationToken).ConfigureAwait(false);

        return new PhotoVolumeCreationParameters
        {
            Share = shareParameters,
            Link = folderParameters,
        };
    }

    private async Task<(ShareCreationParameters Share, LinkCreationParameters Folder)> CreateParametersAsync(CancellationToken cancellationToken)
    {
        const string folderName = "root";

        cancellationToken.ThrowIfCancellationRequested();

        var shareKeyPassphrase = _cryptographyService.GeneratePassphrase();
        var shareKey = _cryptographyService.GenerateShareOrNodeKey();
        var (userEncrypter, address) = await _cryptographyService.CreateMainShareKeyPassphraseEncrypterAsync(cancellationToken).ConfigureAwait(false);

        var shareParameters = GetRootShareCreationParameters(shareKey, shareKeyPassphrase, address, userEncrypter);
        var folderParameters = await GetRootFolderCreationParametersAsync(folderName, shareKey, address.Id, cancellationToken).ConfigureAwait(false);

        return (shareParameters, folderParameters);
    }

    private static ShareCreationParameters GetRootShareCreationParameters(
        PgpPrivateKey shareKey,
        ReadOnlyMemory<byte> shareKeyPassphrase,
        Address address,
        ISigningCapablePgpMessageProducer userEncrypter)
    {
        var (encryptedShareKeyPassphrase, shareKeyPassphraseSignature, _) = userEncrypter.EncryptShareOrNodeKeyPassphrase(shareKeyPassphrase);

        return new ShareCreationParameters
        {
            AddressId = address.Id,
            AddressKeyId = address.GetPrimaryKey().Id,
            Key = shareKey.Lock(shareKeyPassphrase.Span).ToString(),
            Passphrase = encryptedShareKeyPassphrase,
            PassphraseSignature = shareKeyPassphraseSignature,
        };
    }

    private async Task<LinkCreationParameters> GetRootFolderCreationParametersAsync(
        string name,
        PgpPrivateKey shareKey,
        string addressId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (shareEncrypter, _) = await _cryptographyService.CreateNodeNameAndKeyPassphraseEncrypterAsync(shareKey.ToPublic(), addressId, cancellationToken)
            .ConfigureAwait(false);

        var folderKeyPassphrase = _cryptographyService.GeneratePassphrase();
        var folderKey = _cryptographyService.GenerateShareOrNodeKey();
        var (encryptedFolderKeyPassphrase, folderKeyPassphraseSignature, _) = shareEncrypter.EncryptShareOrNodeKeyPassphrase(folderKeyPassphrase);
        var folderHashKey = _cryptographyService.GenerateHashKey();
        var folderHashKeyEncrypter = _cryptographyService.CreateHashKeyEncrypter(folderKey.ToPublic(), folderKey);

        return new LinkCreationParameters
        {
            Name = shareEncrypter.EncryptNodeName(name),
            NodeKey = folderKey.Lock(folderKeyPassphrase.Span).ToString(),
            NodePassphrase = encryptedFolderKeyPassphrase,
            NodePassphraseSignature = folderKeyPassphraseSignature,
            NodeHashKey = folderHashKeyEncrypter.EncryptHashKey(folderHashKey),
        };
    }
}
