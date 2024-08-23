using System;

namespace ProtonDrive.Sync.Adapter.Trees.Adapter.NodeLinking;

public interface INodeLinkRepository<TId>
    where TId : IEquatable<TId>
{
    void Add(NodeLinkType linkType, TId sourceNodeId, TId destinationNodeId);
    void Delete(NodeLinkType linkType, TId sourceNodeId);
    TId? GetSourceNodeIdOrDefault(NodeLinkType linkType, TId destinationNodeId);
    TId? GetDestinationNodeIdOrDefault(NodeLinkType linkType, TId sourceNodeId);
}
