using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ProtonDrive.Sync.Shared.Trees.Collections;

public class AltIdentifiableNodeDictionary<T, TPayload, TId, TAltId> : IdentifiableNodeDictionary<T, TPayload, TId>
    where TPayload : IAltIdentifiable<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly Dictionary<TAltId, T> _nodes = new();

    public bool TryGetByAltId(TAltId altId, [MaybeNullWhen(false)] out T value)
    {
        return _nodes.TryGetValue(altId, out value);
    }

    public override void Add(TPayload payload, T item)
    {
        base.Add(payload, item);
        _nodes.Add(payload.AltId, item);
    }

    public override bool Remove(TPayload payload)
    {
        _nodes.Remove(payload.AltId);
        return base.Remove(payload);
    }

    public override void Update(TPayload prevPayload, TPayload payload, T item)
    {
        base.Update(prevPayload, payload, item);

        if (!prevPayload.AltId.Equals(payload.AltId))
        {
            _nodes.Remove(prevPayload.AltId);
            _nodes.Add(payload.AltId, item);
        }
    }

    public override void Clear()
    {
        _nodes.Clear();
        base.Clear();
    }
}
