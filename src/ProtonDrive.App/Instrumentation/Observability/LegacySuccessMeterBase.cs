using ProtonDrive.App.Mapping;
using ProtonDrive.App.Photos.Import;
using ProtonDrive.App.Settings;
using ProtonDrive.App.Sync;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Instrumentation.Observability;

internal abstract class LegacySuccessMeterBase : ISyncActivityAware, IMappingsAware, IPhotoImportActivityAware
{
    private readonly IReadOnlyDictionary<AttemptRetryShareType, AttemptRetryMonitor<long>> _attemptRetryMonitor;

    private IReadOnlyDictionary<int, MappingType> _mappingTypeById = new Dictionary<int, MappingType>();

    protected LegacySuccessMeterBase(IReadOnlyDictionary<AttemptRetryShareType, AttemptRetryMonitor<long>> attemptRetryMonitor)
    {
        _attemptRetryMonitor = attemptRetryMonitor;
    }

    public abstract bool CanProcessItem(SyncActivityItem<long> item);

    void ISyncActivityAware.OnSyncActivityChanged(SyncActivityItem<long> item)
    {
        if (!CanProcessItem(item))
        {
            return;
        }

        switch (item.Status)
        {
            case SyncActivityItemStatus.Succeeded:
                var shareType = GetShareTypeByMappingId(mappingId: item.RootId);
                _attemptRetryMonitor[shareType].IncrementSuccess(item.Id);
                break;

            case SyncActivityItemStatus.Failed or SyncActivityItemStatus.Warning when !FailureShouldBeIgnored(item.ErrorCode):
                shareType = GetShareTypeByMappingId(mappingId: item.RootId);
                _attemptRetryMonitor[shareType].IncrementFailure(item.Id);
                break;
        }
    }

    void IMappingsAware.OnMappingsChanged(IReadOnlyCollection<RemoteToLocalMapping> activeMappings, IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        _mappingTypeById = activeMappings.Concat(deletedMappings)
            .Where(x => x.Type is not MappingType.SharedWithMeRootFolder)
            .ToDictionary(x => x.Id, x => x.Type)
            .AsReadOnly();
    }

    void IPhotoImportActivityAware.OnPhotoImportActivityChanged(SyncActivityItem<long> item)
    {
        if (!CanProcessItem(item))
        {
            return;
        }

        switch (item.Status)
        {
            case SyncActivityItemStatus.Succeeded:
                _attemptRetryMonitor[AttemptRetryShareType.Photo].IncrementSuccess(item.Id);
                break;

            case SyncActivityItemStatus.Failed or SyncActivityItemStatus.Warning when !FailureShouldBeIgnored(item.ErrorCode):
                _attemptRetryMonitor[AttemptRetryShareType.Photo].IncrementFailure(item.Id);
                break;
        }
    }

    private static bool FailureShouldBeIgnored(FileSystemErrorCode errorCode)
    {
        return errorCode is FileSystemErrorCode.Cancelled
            or FileSystemErrorCode.TooManyChildren
            or FileSystemErrorCode.FreeSpaceExceeded
            or FileSystemErrorCode.NetworkError;
    }

    private static AttemptRetryShareType GetShareType(MappingType mappingType)
    {
        return mappingType switch
        {
            MappingType.CloudFiles => AttemptRetryShareType.Main,
            MappingType.ForeignDevice => AttemptRetryShareType.Device,
            MappingType.HostDeviceFolder => AttemptRetryShareType.Device,
            MappingType.SharedWithMeItem => AttemptRetryShareType.Standard,
            _ => throw new ArgumentOutOfRangeException(nameof(mappingType), mappingType, message: null),
        };
    }

    private AttemptRetryShareType GetShareTypeByMappingId(int? mappingId)
    {
        if (mappingId is null || !_mappingTypeById.TryGetValue(mappingId.Value, out var mappingType))
        {
            return AttemptRetryShareType.Main;
        }

        return GetShareType(mappingType);
    }
}
