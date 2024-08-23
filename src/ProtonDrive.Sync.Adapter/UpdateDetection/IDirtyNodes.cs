using System;
using ProtonDrive.Sync.Adapter.Trees.Adapter;

namespace ProtonDrive.Sync.Adapter.UpdateDetection;

internal interface IDirtyNodes<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    bool BranchIsDirty(AdapterTreeNode<TId, TAltId> node);
}
