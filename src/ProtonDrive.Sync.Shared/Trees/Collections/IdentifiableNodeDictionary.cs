using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ProtonDrive.Shared;

namespace ProtonDrive.Sync.Shared.Trees.Collections;

public class IdentifiableNodeDictionary<T, TPayload, TId> : IEnumerable<T>
    where TPayload : IIdentifiable<TId>
    where TId : IEquatable<TId>
{
    private readonly Dictionary<TId, T> _nodes = new Dictionary<TId, T>();

    public bool TryGet(TId id, [NotNullWhen(true)] out T? value)
    {
        return _nodes.TryGetValue(id, out value!);
    }

    public virtual void Add(TPayload payload, T item)
    {
        _nodes.Add(payload.Id, item);
    }

    public virtual bool Remove(TPayload payload)
    {
        return _nodes.Remove(payload.Id);
    }

    public virtual void Update(TPayload prevPayload, TPayload payload, T item)
    {
    }

    public bool Contains(TPayload payload)
    {
        return _nodes.ContainsKey(payload.Id);
    }

    public virtual void Clear()
    {
        _nodes.Clear();
    }

    public IEnumerator<T> GetEnumerator() => _nodes.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
