using System.Threading;
using System.Threading.Tasks;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Client.Devices.Contracts;
using ProtonDrive.Client.Shares;
using ProtonDrive.Client.Volumes;

namespace ProtonDrive.Client.Devices;

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

        var (shareEncrypter, addressId, addressKey) = await GetShareKeyPassphraseEncrypterAsync(volumeId, cancellationToken).ConfigureAwait(false);

        var (shareKey, shareParameters) = GetShareCreationParameters(shareEncrypter, addressId);

        var linkParameters = GetLinkParameters(name, shareKey.PublicKey, addressKey);

        return new DeviceCreationParameters
        {
            Device = deviceParameters,
            Share = shareParameters,
            Link = linkParameters,
        };
    }

    private DeviceLinkCreationParameters GetLinkParameters(string name, PublicPgpKey shareKey, PrivatePgpKey signatureKey)
    {
        var encrypter = _cryptographyService.CreateNodeNameAndKeyPassphraseEncrypter(shareKey, signatureKey);

        var folderKeyPassphrase = _cryptographyService.GeneratePassphrase();
        var folderKey = _cryptographyService.GenerateShareOrNodeKey(folderKeyPassphrase);
        var (encryptedFolderKeyPassphrase, folderKeyPassphraseSignature, _) = encrypter.EncryptShareOrNodeKeyPassphrase(folderKeyPassphrase);
        var folderHashKey = _cryptographyService.GenerateHashKey();
        var folderHashKeyEncrypter = _cryptographyService.CreateHashKeyEncrypter(folderKey.PublicKey, folderKey);

        return new DeviceLinkCreationParameters
        {
            Name = encrypter.EncryptNodeName(name),
            NodeKey = folderKey.ToString(),
            NodePassphrase = encryptedFolderKeyPassphrase,
            NodePassphraseSignature = folderKeyPassphraseSignature,
            NodeHashKey = folderHashKeyEncrypter.EncryptHashKey(folderHashKey),
        };
    }

    private (PrivatePgpKey PrivateKey, DeviceShareCreationParameters DeviceParameters) GetShareCreationParameters(
        ISigningCapablePgpMessageProducer encrypter,
        string addressId)
    {
        var shareKeyPassphrase = _cryptographyService.GeneratePassphrase();
        var shareKey = _cryptographyService.GenerateShareOrNodeKey(shareKeyPassphrase);

        var (encryptedShareKeyPassphrase, shareKeyPassphraseSignature, _) = encrypter.EncryptShareOrNodeKeyPassphrase(shareKeyPassphrase);

        return (shareKey, new DeviceShareCreationParameters
        {
            AddressId = addressId,
            Key = shareKey.ToString(),
            Passphrase = encryptedShareKeyPassphrase,
            PassphraseSignature = shareKeyPassphraseSignature,
        });
    }

    private async Task<(ISigningCapablePgpMessageProducer Encrypter, string AddressId, PrivatePgpKey AddressKey)> GetShareKeyPassphraseEncrypterAsync(
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

        return (encrypter, shareResponse.AddressId, address.GetPrimaryKey().PrivateKey);
    }
}
