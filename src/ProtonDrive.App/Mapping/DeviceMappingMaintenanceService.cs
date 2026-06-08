using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Devices;
using ProtonDrive.App.Services;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.FileSystem.Integration;

namespace ProtonDrive.App.Mapping;

/// <summary>
/// Creates and maintains foreign device mappings and removes orphaned host device folder mappings.
/// </summary>
internal sealed class DeviceMappingMaintenanceService : IStoppableService, IDeviceServiceStateAware, IDevicesAware, IMappingsAware
{
    private readonly ISyncFolderPathProvider _syncFolderPathProvider;
    private readonly ILocalFolderService _localFolderService;
    private readonly IMappingRegistry _mappingRegistry;
    private readonly INumberSuffixedNameGenerator _numberSuffixedNameGenerator;
    private readonly ILogger<DeviceMappingMaintenanceService> _logger;
    private readonly CoalescingAction _mappingMaintenance;

    private ImmutableList<Device> _devices = [];
    private DeviceServiceStatus _deviceServiceStatus = DeviceServiceStatus.Idle;
    private volatile bool _stopping;
    private volatile bool _hasReceivedMappings;
    private bool _mappingsModificationIsInProgress;

    public DeviceMappingMaintenanceService(
        ISyncFolderPathProvider syncFolderPathProvider,
        ILocalFolderService localFolderService,
        IMappingRegistry mappingRegistry,
        INumberSuffixedNameGenerator numberSuffixedNameGenerator,
        ILogger<DeviceMappingMaintenanceService> logger)
    {
        _syncFolderPathProvider = syncFolderPathProvider;
        _localFolderService = localFolderService;
        _mappingRegistry = mappingRegistry;
        _numberSuffixedNameGenerator = numberSuffixedNameGenerator;
        _logger = logger;

        _mappingMaintenance = _logger.GetCoalescingActionWithExceptionsLoggingAndCancellationHandling(MaintainMappingsAsync, nameof(DeviceMappingMaintenanceService));
    }

    Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _stopping = true;
        _mappingMaintenance.Cancel();

        return WaitForCompletionAsync();
    }

    void IDeviceServiceStateAware.OnDeviceServiceStateChanged(DeviceServiceStatus status)
    {
        _deviceServiceStatus = status;

        if (status is DeviceServiceStatus.Succeeded)
        {
            ScheduleMappingMaintenance();
        }
    }

    void IDevicesAware.OnDeviceChanged(DeviceChangeType changeType, Device device)
    {
        switch (changeType)
        {
            case DeviceChangeType.Added:
                _devices = _devices.Add(device);
                break;

            case DeviceChangeType.Updated:
                break;

            case DeviceChangeType.Removed:
                _devices = _devices.Remove(device);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(changeType), changeType, message: null);
        }

        /* Device change is notified while device service status is SettingUp.
         There is no need to schedule device mapping maintenance here, as it will
         be scheduled upon the device service status becomes Succeeded. */
    }

    void IMappingsAware.OnMappingsChanged(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        if (_mappingsModificationIsInProgress)
        {
            return;
        }

        _hasReceivedMappings = true;

        ScheduleMappingMaintenance();
    }

    internal Task WaitForCompletionAsync()
    {
        return _mappingMaintenance.WaitForCompletionAsync();
    }

    private static string GetLocalName(RemoteToLocalMapping mapping)
    {
        return Path.GetFileName(mapping.Local.Path);
    }

    private static RemoteToLocalMapping CreateForeignDeviceMapping(Device device, string localFolderName, string foreignDevicesFolderPath)
    {
        return new RemoteToLocalMapping
        {
            Type = MappingType.ForeignDevice,
            SyncMethod = SyncMethod.OnDemand,
            Local =
            {
                Path = Path.Combine(foreignDevicesFolderPath, localFolderName),
            },
            Remote =
            {
                VolumeId = device.DataItem.VolumeId,
                ShareId = device.DataItem.ShareId,
                RootLinkId = device.DataItem.LinkId,
                RootItemName = device.DataItem.Name,
            },
        };
    }

    private void ScheduleMappingMaintenance()
    {
        if (_stopping)
        {
            return;
        }

        _mappingMaintenance.Cancel();
        _mappingMaintenance.Run();
    }

    private async Task MaintainMappingsAsync(CancellationToken cancellationToken)
    {
        if (_stopping || !_hasReceivedMappings)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebug("Started maintaining foreign device mappings");

        using var mappings = await _mappingRegistry.GetMappingsAsync(cancellationToken).ConfigureAwait(false);

        var activeMappings = mappings.GetActive();

        var cloudFilesMapping = activeMappings.FirstOrDefault(m => m.Type is MappingType.CloudFiles);

        var previousDeviceMappings = activeMappings.Where(m => m.Type is MappingType.ForeignDevice).ToList().AsReadOnly();

        var actualDeviceMappings = previousDeviceMappings
            .Where(m =>
                /* If there is no cloud files mapping, all foreign device mappings are removed */
                cloudFilesMapping != null &&
                /* Foreign device mappings, that were added before adding the cloud files mapping,
                 are removed. The removal is based on assumption, that mapping IDs get monotonically
                 increasing values.*/
                m.Id > cloudFilesMapping.Id)
            .ToList()
            .AsReadOnly();

        IReadOnlyCollection<RemoteToLocalMapping> deviceMappingsToKeep = [];
        var numberOfAddedMappings = 0;
        var numberOfDeletedMappings = 0;

        var foreignDevicesFolderPath = _syncFolderPathProvider.GetForeignDevicesFolderPath();

        if (_deviceServiceStatus is not DeviceServiceStatus.Succeeded)
        {
            _logger.LogInformation("Unable to maintain foreign device mappings until device service succeeds");

            deviceMappingsToKeep = actualDeviceMappings;
        }
        else if (cloudFilesMapping != null && !string.IsNullOrEmpty(foreignDevicesFolderPath))
        {
            var namesInUse = actualDeviceMappings
                .Select(GetLocalName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var mappingsToKeep = new List<RemoteToLocalMapping>();

            foreach (var device in _devices)
            {
                if (device.Type != DeviceType.Foreign)
                {
                    continue;
                }

                if (!device.DataItem.IsSynchronizationEnabled)
                {
                    _logger.LogWarning("Device with ID {DeviceId} has syncing disabled, but we sync it regardless", device.Id);
                }

                // The device name is not used for comparison, so renaming
                // the device does not trigger removal of the device mapping.
                var existingMapping = actualDeviceMappings.FirstOrDefault(
                    m =>
                        m.Remote.VolumeId == device.DataItem.VolumeId &&
                        m.Remote.ShareId == device.DataItem.ShareId &&
                        m.Remote.RootLinkId == device.DataItem.LinkId);

                if (existingMapping is not null)
                {
                    mappingsToKeep.Add(existingMapping);
                }
                else
                {
                    var uniqueFolderName = GetUniqueName(device.Name, namesInUse, foreignDevicesFolderPath);

                    mappings.Add(CreateForeignDeviceMapping(device, uniqueFolderName, foreignDevicesFolderPath));
                    numberOfAddedMappings++;
                }
            }

            deviceMappingsToKeep = mappingsToKeep.AsReadOnly();
        }

        foreach (var mapping in previousDeviceMappings.Except(deviceMappingsToKeep))
        {
            mappings.Delete(mapping);
            numberOfDeletedMappings++;
        }

        if (_deviceServiceStatus is DeviceServiceStatus.Succeeded)
        {
            DeleteOrphanedHostDeviceFolderMappings(activeMappings, mappings);
        }

        try
        {
            _mappingsModificationIsInProgress = true;

            mappings.SaveAndNotify();
        }
        finally
        {
            _mappingsModificationIsInProgress = false;
        }

        _logger.LogInformation(
            "Finished maintaining foreign device mappings: {NumberOfAddedMapping} added, {NumberOfDeletedMappings} deleted",
            numberOfAddedMappings,
            numberOfDeletedMappings);
    }

    private void DeleteOrphanedHostDeviceFolderMappings(IReadOnlyCollection<RemoteToLocalMapping> activeMappings, IUpdatableMappings mappings)
    {
        var hostDevice = _devices.FirstOrDefault(d => d.Type is DeviceType.Host && !string.IsNullOrEmpty(d.Id));

        var orphanedMappings = activeMappings
            .Where(m => m.Type is MappingType.HostDeviceFolder
                && !string.IsNullOrEmpty(m.Remote.ShareId)
                && (hostDevice is null || hostDevice.ShareId != m.Remote.ShareId));

        foreach (var mapping in orphanedMappings)
        {
            _logger.LogInformation(
                "Deleting host device folder mapping {Id}: Host device with Share ID \"{ShareId}\" no longer exists on remote",
                mapping.Id,
                mapping.Remote.ShareId);

            mappings.Delete(mapping);
        }
    }

    private string GetUniqueName(string name, HashSet<string> namesInUse, string parentPath)
    {
        var uniqueName = _numberSuffixedNameGenerator
            .GenerateNames(name, NameType.Folder)
            .First(
                candidateName =>
                {
                    var itemPath = Path.Combine(parentPath, candidateName);
                    return !namesInUse.Contains(candidateName)
                        && !_localFolderService.FolderExists(itemPath)
                        && !_localFolderService.FileExists(itemPath);
                });

        namesInUse.Add(uniqueName);

        return uniqueName;
    }
}
