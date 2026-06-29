using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;

internal interface IDirtyNodes<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    bool BranchIsDirty(AdapterTreeNode<TId, TAltId> node);
}
