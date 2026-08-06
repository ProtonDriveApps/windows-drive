using Proton.Drive.Shared;

namespace Proton.Drive.Sdk.Sync.Shared.Trees;

public interface IIdentifiableTreeNode<TId> : IIdentifiable<TId>
    where TId : IEquatable<TId>
{
    TId ParentId { get; set; }
}
