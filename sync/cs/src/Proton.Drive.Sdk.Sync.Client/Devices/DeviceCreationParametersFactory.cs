using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Cryptography;
using Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Devices.Contracts;
using Proton.Drive.Sdk.Sync.Client.Shares;
using Proton.Drive.Sdk.Sync.Client.Shares.Contracts;
using Proton.Drive.Sdk.Sync.Client.Volumes;
using Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Devices;

internal class DeviceCreationParametersFactory : IDeviceCreationParametersFactory
{
    private readonly ICryptographyService _cryptographyService;
    private readonly IVolumeApiClient _volumeApiClient;
    private readonly IShareApiClient _shareApiClient;

    public DeviceCreationParametersFactory(
        ICryptographyService cryptographyService,
        IVolumeApiClient volumeApiClient,
        IShareApiClient shareApiClient)
    {
        _cryptographyService = cryptographyService;
        _volumeApiClient = volumeApiClient;
        _shareApiClient = shareApiClient;
    }

    public async Task<DeviceCreationParameters> CreateAsync(string volumeId, string name, CancellationToken cancellationToken)
    {
        var deviceParameters = new DeviceDeviceCreationParameters
        {
            Type = DevicePlatform.Windows,
            VolumeId = volumeId,
            IsSynchronizationEnabled = true,
        };

        var (shareEncrypter, address) = await GetShareKeyPassphraseEncrypterAsync(volumeId, cancellationToken).ConfigureAwait(false);

        var (shareKey, shareParameters) = GetRootShareCreationParameters(shareEncrypter, address);

        var folderParameters = GetRootFolderCreationParameters(name, shareKey.ToPublic(), address.GetPrimaryKey().PrivateKey);

        return new DeviceCreationParameters
        {
            Device = deviceParameters,
            Share = shareParameters,
            Link = folderParameters,
        };
    }

    private LinkCreationParameters GetRootFolderCreationParameters(string name, PgpPublicKey shareKey, PgpPrivateKey signatureKey)
    {
        var encrypter = _cryptographyService.CreateNodeNameAndKeyPassphraseEncrypter(shareKey, signatureKey);

        var folderKeyPassphrase = _cryptographyService.GeneratePassphrase();
        var folderKey = _cryptographyService.GenerateShareOrNodeKey();
        var (encryptedFolderKeyPassphrase, folderKeyPassphraseSignature, _) = encrypter.EncryptShareOrNodeKeyPassphrase(folderKeyPassphrase);
        var folderHashKey = _cryptographyService.GenerateHashKey();
        var folderHashKeyEncrypter = _cryptographyService.CreateHashKeyEncrypter(folderKey.ToPublic(), folderKey);

        return new LinkCreationParameters
        {
            Name = encrypter.EncryptNodeName(name),
            NodeKey = folderKey.Lock(folderKeyPassphrase.Span).ToString(),
            NodePassphrase = encryptedFolderKeyPassphrase,
            NodePassphraseSignature = folderKeyPassphraseSignature,
            NodeHashKey = folderHashKeyEncrypter.EncryptHashKey(folderHashKey),
        };
    }

    private (PgpPrivateKey PrivateKey, ShareCreationParameters DeviceParameters) GetRootShareCreationParameters(
        ISigningCapablePgpMessageProducer encrypter,
        Address address)
    {
        var shareKeyPassphrase = _cryptographyService.GeneratePassphrase();
        var shareKey = _cryptographyService.GenerateShareOrNodeKey();

        var (encryptedShareKeyPassphrase, shareKeyPassphraseSignature, _) = encrypter.EncryptShareOrNodeKeyPassphrase(shareKeyPassphrase);

        return (shareKey, new ShareCreationParameters
        {
            AddressId = address.Id,
            AddressKeyId = address.GetPrimaryKey().Id,
            Key = shareKey.Lock(shareKeyPassphrase.Span).ToString(),
            Passphrase = encryptedShareKeyPassphrase,
            PassphraseSignature = shareKeyPassphraseSignature,
        });
    }

    private async Task<(ISigningCapablePgpMessageProducer Encrypter, Address Address)> GetShareKeyPassphraseEncrypterAsync(
        string volumeId,
        CancellationToken cancellationToken)
    {
        var volumeResponse = await _volumeApiClient.GetVolumeAsync(volumeId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        if (volumeResponse.Volume.State is not VolumeState.Active)
        {
            throw new ApiException(ResponseCode.InvalidRequirements, $"Volume state is {volumeResponse.Volume.State}");
        }

        var mainShareId = volumeResponse.Volume.Share.Id;

        var shareResponse = await _shareApiClient.GetShareAsync(mainShareId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        if (shareResponse.IsLocked || string.IsNullOrEmpty(shareResponse.AddressId))
        {
            // Address ID can be NULL when the share is locked
            throw new ApiException(ResponseCode.InvalidRequirements, "Volume main share is locked");
        }

        var (encrypter, address) = await _cryptographyService.CreateShareKeyPassphraseEncrypterAsync(shareResponse.AddressId, cancellationToken)
            .ConfigureAwait(false);

        return (encrypter, address);
    }
}
