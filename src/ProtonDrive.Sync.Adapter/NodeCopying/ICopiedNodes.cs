using System;
using ProtonDrive.Sync.Adapter.Trees.Adapter;

namespace ProtonDrive.Sync.Adapter.NodeCopying;

internal interface ICopiedNodes<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    AdapterTreeNode<TId, TAltId>? GetSourceNodeOrDefault(TId destinationNodeId);

    void Add(AdapterTreeNode<TId, TAltId> sourceNode, AdapterTreeNode<TId, TAltId> destinationNode);

    void RemoveLinksInBranch(TId nodeId);
}
