using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Propagation;
using Proton.Drive.Sdk.Sync.Shared;

namespace Proton.Drive.Sdk.Sync.Engine.Propagation;

internal sealed record FileTransfer<TId>(
    Replica Replica,
    PropagationTreeNode<TId> Node,
    PropagationTreeNodeModel<TId> NodeModel,
    int NumberOfRetries = 0)
    where TId : IEquatable<TId>;
