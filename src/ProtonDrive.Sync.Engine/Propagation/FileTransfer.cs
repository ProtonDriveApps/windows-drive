using System;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Shared;

namespace ProtonDrive.Sync.Engine.Propagation;

internal sealed record FileTransfer<TId>(
    Replica Replica,
    PropagationTreeNode<TId> Node,
    PropagationTreeNodeModel<TId> NodeModel,
    int NumberOfRetries = 0)
    where TId : IEquatable<TId>;
