using System;

namespace ProtonDrive.Sync.Shared.Trees;

public interface IAltIdentifiableTreeNodeRepository<T, TId, TAltId> : ITreeNodeRepository<T, TId>
    where T : class, IIdentifiableTreeNode<TId>, IAltIdentifiable<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    TAltId? GetLastAltId();

    T? NodeByAltId(TAltId altId);
}
