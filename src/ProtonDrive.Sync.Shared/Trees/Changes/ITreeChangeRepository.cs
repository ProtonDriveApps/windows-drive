using System;
using System.Collections.Generic;

namespace ProtonDrive.Sync.Shared.Trees.Changes;

public interface ITreeChangeRepository<TId>
    where TId : IEquatable<TId>
{
    IEnumerable<TreeChange<TId>> GetAll();
    bool ContainsNode(TId id);
    void Add(TreeChange<TId> item);
    void DeleteUpTo(TId id);
    TId? GetLastNodeId();
}
