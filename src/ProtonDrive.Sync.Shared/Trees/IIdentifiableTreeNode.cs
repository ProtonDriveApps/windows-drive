using System;
using ProtonDrive.Shared;

namespace ProtonDrive.Sync.Shared.Trees;

public interface IIdentifiableTreeNode<TId> : IIdentifiable<TId>
    where TId : IEquatable<TId>
{
    TId ParentId { get; set; }
}
