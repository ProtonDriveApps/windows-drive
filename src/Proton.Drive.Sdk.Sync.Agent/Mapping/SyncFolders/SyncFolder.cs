using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;

public sealed class SyncFolder
{
    private MappingState _state = MappingState.None;

    internal SyncFolder(RemoteToLocalMapping mapping)
    {
        Mapping = mapping;
        Type = ToSyncFolderType(mapping.Type);
        LocalPath = GetLocalPath();
        RootLinkType = mapping.Remote.RootItemType;
    }

    public LinkType RootLinkType { get; }
    public SyncFolderType Type { get; }
    public SyncMethod SyncMethod => Mapping.SyncMethod;
    public string LocalPath { get; }
    public int MappingId => Mapping.Id;
    public string? RemoteName => Mapping.Remote.RootItemName;
    public string? RemoteShareId => Mapping.Remote.ShareId;
    public bool RemoteIsReadOnly => Mapping.Remote.IsReadOnly;
    public MappingSetupStatus Status => _state.Status;
    public MappingErrorCode ErrorCode => _state.ErrorCode;
    public bool IsStorageOptimizationEnabled => Mapping.Local.StorageOptimization?.IsEnabled ?? false;
    public StorageOptimizationStatus StorageOptimizationStatus => Mapping.Local.StorageOptimization?.Status ?? StorageOptimizationStatus.Succeeded;
    public StorageOptimizationErrorCode StorageOptimizationErrorCode => Mapping.Local.StorageOptimization?.ErrorCode ?? StorageOptimizationErrorCode.None;
    public string? ConflictingProviderName => Mapping.Local.StorageOptimization?.ConflictingProviderName;

    internal RemoteToLocalMapping Mapping { get; }

    internal bool SetState(MappingState state)
    {
        var result = !state.Equals(_state);
        _state = state;

        return result;
    }

    private static SyncFolderType ToSyncFolderType(MappingType mappingType)
    {
        return mappingType switch
        {
            MappingType.CloudFiles => SyncFolderType.AccountRoot,
            MappingType.HostDeviceFolder => SyncFolderType.HostDeviceFolder,
            MappingType.ForeignDevice => SyncFolderType.ForeignDevice,
            MappingType.SharedWithMeRootFolder => SyncFolderType.SharedWithMeRoot,
            MappingType.SharedWithMeItem => SyncFolderType.SharedWithMeItem,
            _ => throw new ArgumentOutOfRangeException(nameof(mappingType)),
        };
    }

    private string GetLocalPath()
    {
        return Mapping.Type is MappingType.CloudFiles
            ? Mapping.TryGetAccountRootFolderPath(out var path) ? path : string.Empty
            : Mapping.Local.Path;
    }
}
