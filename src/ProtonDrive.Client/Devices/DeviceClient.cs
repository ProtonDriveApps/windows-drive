using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Client.Devices.Contracts;
using ProtonDrive.Client.RemoteNodes;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Text;

namespace ProtonDrive.Client.Devices;

internal sealed class DeviceClient : IDeviceClient
{
    private readonly IDeviceApiClient _deviceApiClient;
    private readonly ILinkApiClient _linkApiClient;
    private readonly IRemoteNodeService _remoteNodeService;
    private readonly IDeviceCreationParametersFactory _parametersFactory;
    private readonly ICryptographyService _cryptographyService;

    private readonly RandomStringGenerator _randomStringGenerator = new(RandomStringCharacterGroup.NumbersAndLatinLowercase);

    public DeviceClient(
        IDeviceApiClient deviceApiClient,
        ILinkApiClient linkApiClient,
        IRemoteNodeService remoteNodeService,
        IDeviceCreationParametersFactory parametersFactory,
        ICryptographyService cryptographyService)
    {
        _deviceApiClient = deviceApiClient;
        _linkApiClient = linkApiClient;
        _remoteNodeService = remoteNodeService;
        _parametersFactory = parametersFactory;
        _cryptographyService = cryptographyService;
    }

    public async Task<IReadOnlyCollection<Device>> GetAllAsync(CancellationToken cancellationToken)
    {
        var deviceListResponse = await _deviceApiClient.GetAllAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        var getDeviceNameTasks = deviceListResponse.Devices.Select(GetDeviceAsync);

        return await Task.WhenAll(getDeviceNameTasks).ConfigureAwait(false);

        async Task<Device> GetDeviceAsync(DeviceListItem device)
        {
            if (!string.IsNullOrEmpty(device.Share.Name))
            {
                return ToDevice(device, device.Share.Name);
            }

            var node = await GetRemoteNodeAsync(device.Share.Id, device.Share.LinkId, cancellationToken).ConfigureAwait(false);

            return ToDevice(device, node.Name);
        }
    }

    public async Task<Device> CreateAsync(string volumeId, string name, CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(volumeId, nameof(volumeId));
        Ensure.NotNullOrEmpty(name, nameof(name));

        var parameters = await _parametersFactory.CreateAsync(volumeId, name, cancellationToken).ConfigureAwait(false);

        var result = await _deviceApiClient.CreateAsync(parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        var device = result.Device;
        if (string.IsNullOrEmpty(device.Id) || string.IsNullOrEmpty(device.ShareId) || string.IsNullOrEmpty(device.LinkId))
        {
            throw new ApiException("API returned invalid data");
        }

        return new Device
        {
            Id = device.Id,
            VolumeId = volumeId,
            Platform = DevicePlatform.Windows,
            ShareId = device.ShareId,
            LinkId = device.LinkId,
            Name = name,
            IsSynchronizationEnabled = true,
        };
    }

    public async Task<Device> RenameAsync(Device device, string name, CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(device.Id, nameof(device), nameof(device.Id));
        Ensure.NotNullOrEmpty(device.LinkId, nameof(device), nameof(device.LinkId));
        Ensure.NotNullOrEmpty(device.ShareId, nameof(device), nameof(device.ShareId));
        Ensure.NotNullOrEmpty(name, nameof(name));

        await RenameDeviceLinkAsync(device, name, cancellationToken).ConfigureAwait(false);

        // Previously, the unencrypted share name was used to save the device name.
        await ClearDeviceShareNameAsync(device, cancellationToken).ConfigureAwait(false);

        return device with
        {
            Name = name,
        };
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(id, nameof(id));

        await _deviceApiClient.DeleteAsync(id, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
    }

    private async Task<RemoteNode> GetRemoteNodeAsync(string shareId, string linkId, CancellationToken cancellationToken)
    {
        var linkResponse = await _linkApiClient.GetLinkAsync(shareId, linkId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        var link = linkResponse.Link ?? throw new ApiException(ResponseCode.InvalidValue, $"Could not get link with ID \"{linkId}\"");

        var node = await _remoteNodeService.GetRemoteNodeAsync(shareId, link, cancellationToken).ConfigureAwait(false);

        return node;
    }

    private async Task RenameDeviceLinkAsync(Device device, string name, CancellationToken cancellationToken)
    {
        var node = await GetRemoteNodeAsync(device.ShareId, device.LinkId, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(node.ParentId))
        {
            throw new ApiException(ResponseCode.InvalidValue, $"Device link with ID=\"{device.LinkId}\" is not a root link.");
        }

        var share = await _remoteNodeService.GetShareAsync(device.ShareId, cancellationToken).ConfigureAwait(false);

        var (nameEncrypter, address) = await _cryptographyService.CreateNodeNameAndKeyPassphraseEncrypterAsync(
            share.Key.PublicKey,
            node.NameSessionKey,
            share.RelevantMembershipAddressId,
            cancellationToken).ConfigureAwait(false);

        var encryptedName = nameEncrypter.EncryptNodeName(name);

        var renameLinkParameters = new RenameLinkParameters
        {
            Name = encryptedName,
            /* Until the backend will be updated to not require name hash when renaming root link, the random string is provided */
            NameHash = _randomStringGenerator.GenerateRandomString(24),
            NameSignatureEmailAddress = address.EmailAddress,
            OriginalNameHash = node.NameHash,
        };

        await _linkApiClient.RenameLinkAsync(device.ShareId, device.LinkId, renameLinkParameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
    }

    private async Task ClearDeviceShareNameAsync(Device device, CancellationToken cancellationToken)
    {
        var deviceUpdateParameters = new DeviceUpdateParameters
        {
            Share =
            {
                Name = string.Empty,
            },
        };

        await _deviceApiClient.UpdateAsync(device.Id, deviceUpdateParameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
    }

    private static Device ToDevice(DeviceListItem item, string deviceName)
    {
        return new Device
        {
            Id = item.Device.Id,
            VolumeId = item.Device.VolumeId,
            Platform = item.Device.Platform,
            ShareId = item.Share.Id,
            LinkId = item.Share.LinkId,
            Name = deviceName,
            IsSynchronizationEnabled = item.Device.IsSynchronizationEnabled,
        };
    }
}
