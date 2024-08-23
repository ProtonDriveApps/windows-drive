using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Devices;
using ProtonDrive.App.Mapping.Setup;
using ProtonDrive.App.Services;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Mapping;

/// <summary>
/// Creates and maintains foreign device mappings
/// </summary>
internal sealed class DeviceMappingMaintenanceService : IStoppableService, IDeviceServiceStateAware, IDevicesAware, IMappingsAware
{
    private readonly ISyncFolderPathProvider _syncFolderPathProvider;
    private readonly IMappingRegistry _mappingRegistry;
    private readonly ILogger<DeviceMappingMaintenanceService> _logger;
    private readonly CoalescingAction _mappingMaintenance;

    private ImmutableList<Device> _devices = [];
    private DeviceServiceStatus _deviceServiceStatus = DeviceServiceStatus.Idle;
    private volatile bool _stopping;
    private bool _mappingsModificationIsInProgress;

    public DeviceMappingMaintenanceService(
        ISyncFolderPathProvider syncFolderPathProvider,
        IMappingRegistry mappingRegistry,
        ILogger<DeviceMappingMaintenanceService> logger)
    {
        _syncFolderPathProvider = syncFolderPathProvider;
        _mappingRegistry = mappingRegistry;
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

        ScheduleMappingMaintenance();
    }

    internal Task WaitForCompletionAsync()
    {
        // Wait for all scheduled tasks to complete
        return _mappingMaintenance.CurrentTask;
    }

    private static RemoteToLocalMapping CreateForeignDeviceMapping(Device device, string localFolderName, string foreignDevicesFolderPath)
    {
        return new RemoteToLocalMapping
        {
            Type = MappingType.ForeignDevice,
            SyncMethod = SyncMethod.OnDemand,
            Local =
            {
                RootFolderPath = Path.Combine(foreignDevicesFolderPath, localFolderName),
            },
            Remote =
            {
                VolumeId = device.DataItem.VolumeId,
                ShareId = device.DataItem.ShareId,
                RootLinkId = device.DataItem.LinkId,
                RootFolderName = device.DataItem.Name,
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
        if (_stopping)
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
            var uniqueLocalFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var deviceMapping in actualDeviceMappings)
            {
                var folderName = Path.GetFileName(deviceMapping.Local.RootFolderPath);
                uniqueLocalFolderNames.Add(folderName);
            }

            var mappingsToKeep = new List<RemoteToLocalMapping>();

            foreach (var device in _devices)
            {
                if (device.Type != DeviceType.Foreign || !device.DataItem.IsSynchronizationEnabled)
                {
                    continue;
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
                    var uniqueFolderName = GetUniqueFolderName(device.Name);

                    mappings.Add(CreateForeignDeviceMapping(device, uniqueFolderName, foreignDevicesFolderPath));
                    numberOfAddedMappings++;
                }
            }

            deviceMappingsToKeep = mappingsToKeep.AsReadOnly();

            string GetUniqueFolderName(string deviceName)
            {
                var nameGenerator = new NumberSuffixedNameGenerator(deviceName, NameType.Folder);
                var uniqueFolderName = nameGenerator.GenerateNames().First(x => !uniqueLocalFolderNames.Contains(x));

                uniqueLocalFolderNames.Add(uniqueFolderName);
                return uniqueFolderName;
            }
        }

        foreach (var mapping in previousDeviceMappings.Except(deviceMappingsToKeep))
        {
            mappings.Delete(mapping);
            numberOfDeletedMappings++;
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
}
