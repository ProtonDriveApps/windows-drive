using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Adapter.Shared;

internal static class SyncStateMaintenanceExtensions
{
    public static AdapterNodeStatus GetStateUpdateFlags<TAltId>(this PlaceholderState placeholderState, FileAttributes attributes, RootInfo<TAltId> rootInfo)
        where TAltId : IEquatable<TAltId>
    {
        var isDehydrationRequested = !attributes.HasFlag(FileAttributes.Directory) && attributes.IsDehydrationRequested() && !placeholderState.HasFlag(PlaceholderState.PartiallyOnDisk);
        var isPinned = !attributes.HasFlag(FileAttributes.Directory) && attributes.IsPinned() && placeholderState.HasFlag(PlaceholderState.Partial);
        var isStateUpdatePending = rootInfo.IsOnDemand
                                   && !attributes.IsExcluded()
                                   && !placeholderState.HasFlag(PlaceholderState.Invalid)
                                   && (!placeholderState.HasFlag(PlaceholderState.InSync)
                                       || isDehydrationRequested
                                       || isPinned);

        var isHydrationPending = isStateUpdatePending && isPinned;

        return AdapterNodeStatus.None
               | (isStateUpdatePending ? AdapterNodeStatus.StateUpdatePending : AdapterNodeStatus.None)
               | (isHydrationPending ? AdapterNodeStatus.HydrationPending : AdapterNodeStatus.None);
    }
}
