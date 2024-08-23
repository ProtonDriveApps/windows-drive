using System;
using System.Threading;
using System.Threading.Tasks;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Cryptography;

namespace ProtonDrive.Client.Volumes;

internal class VolumeCreationParametersFactory : IVolumeCreationParametersFactory
{
    private readonly ICryptographyService _cryptographyService;

    public VolumeCreationParametersFactory(ICryptographyService cryptographyService)
    {
        _cryptographyService = cryptographyService;
    }

    public async Task<VolumeCreationParameters> CreateAsync(CancellationToken cancellationToken)
    {
        const string folderName = "root";
        const string volumeName = "MainVolume";
        const string shareName = "MainShare";

        var parameters = new VolumeCreationParameters();

        var shareKeyPassphrase = _cryptographyService.GeneratePassphrase();
        var shareKey = _cryptographyService.GenerateShareOrNodeKey(shareKeyPassphrase);
        var (userEncrypter, address) = await _cryptographyService.CreateMainShareKeyPassphraseEncrypterAsync(cancellationToken).ConfigureAwait(false);

        SetShareParameters(parameters, volumeName, shareName, shareKey, shareKeyPassphrase, address.Id, userEncrypter);

        await SetFolderParameters(parameters, shareKey, folderName, address.Id, cancellationToken).ConfigureAwait(false);

        return parameters;
    }

    private static void SetShareParameters(
        VolumeCreationParameters parameters,
        string volumeName,
        string shareName,
        PrivatePgpKey shareKey,
        ReadOnlyMemory<byte> shareKeyPassphrase,
        string addressId,
        ISigningCapablePgpMessageProducer userEncrypter)
    {
        var (encryptedShareKeyPassphrase, shareKeyPassphraseSignature, _) = userEncrypter.EncryptShareOrNodeKeyPassphrase(shareKeyPassphrase);

        parameters.AddressId = addressId;
        parameters.VolumeName = volumeName;
        parameters.ShareName = shareName;
        parameters.ShareKey = shareKey.ToString();
        parameters.SharePassphrase = encryptedShareKeyPassphrase;
        parameters.SharePassphraseSignature = shareKeyPassphraseSignature;
    }

    private async Task SetFolderParameters(
        VolumeCreationParameters parameters,
        PrivatePgpKey shareKey,
        string folderName,
        string addressId,
        CancellationToken cancellationToken)
    {
        var (shareEncrypter, _) = await _cryptographyService.CreateNodeNameAndKeyPassphraseEncrypterAsync(shareKey.PublicKey, addressId, cancellationToken)
            .ConfigureAwait(false);

        var folderKeyPassphrase = _cryptographyService.GeneratePassphrase();
        var folderKey = _cryptographyService.GenerateShareOrNodeKey(folderKeyPassphrase);
        var (encryptedFolderKeyPassphrase, folderKeyPassphraseSignature, _) = shareEncrypter.EncryptShareOrNodeKeyPassphrase(folderKeyPassphrase);
        var folderHashKey = _cryptographyService.GenerateHashKey();
        var folderHashKeyEncrypter = _cryptographyService.CreateHashKeyEncrypter(folderKey.PublicKey, folderKey);

        parameters.FolderName = shareEncrypter.EncryptNodeName(folderName);
        parameters.FolderKey = folderKey.ToString();
        parameters.FolderPassphrase = encryptedFolderKeyPassphrase;
        parameters.FolderPassphraseSignature = folderKeyPassphraseSignature;
        parameters.FolderHashKey = folderHashKeyEncrypter.EncryptHashKey(folderHashKey);
    }
}
