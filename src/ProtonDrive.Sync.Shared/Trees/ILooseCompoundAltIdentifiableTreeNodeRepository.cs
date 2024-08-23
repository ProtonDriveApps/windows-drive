using System;

namespace ProtonDrive.Sync.Shared.Trees;

public interface ILooseCompoundAltIdentifiableTreeNodeRepository<T, TId, TAltId> : IAltIdentifiableTreeNodeRepository<T, TId, LooseCompoundAltIdentity<TAltId>>
    where T : class, IIdentifiableTreeNode<TId>, ILooseCompoundAltIdentifiable<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
}
