using System;
using System.Collections.Generic;

namespace ProtonDrive.Sync.Shared.Trees;

public interface ITreeNodeRepository<T, TId>
    where T : class, IIdentifiableTreeNode<TId>
    where TId : IEquatable<TId>
{
    TId? GetLastId();
    T? NodeById(TId id);

    void Create(T node);
    void Update(T node);
    void Delete(T node);
    void DeleteChildren(TId nodeId);
    void Clear();

    IEnumerable<T> Children(T node);
    IEnumerable<TId> ChildrenIds(TId nodeId);
}
