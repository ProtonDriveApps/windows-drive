using System;
using System.Collections.Generic;

namespace ProtonDrive.Sync.Shared.Trees.Collections;

public class LooseCompoundAltIdentifiableNodeDictionary<T, TPayload, TId, TAltId> : IdentifiableNodeDictionary<T, TPayload, TId>
    where TPayload : IAltIdentifiable<TId, LooseCompoundAltIdentity<TAltId>>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly Dictionary<LooseCompoundAltIdentity<TAltId>, T> _nodes = new();

    public bool TryGetByAltId(LooseCompoundAltIdentity<TAltId> altId, out T? value)
    {
        if (altId.IsDefault())
        {
            throw new ArgumentOutOfRangeException(nameof(altId), $"Argument {nameof(altId)} value must be not default");
        }

        return _nodes.TryGetValue(altId, out value);
    }

    public override void Add(TPayload payload, T item)
    {
        base.Add(payload, item);

        if (!payload.AltId.IsDefault())
        {
            _nodes.Add(payload.AltId, item);
        }
    }

    public override bool Remove(TPayload payload)
    {
        if (!payload.AltId.IsDefault())
        {
            _nodes.Remove(payload.AltId);
        }

        return base.Remove(payload);
    }

    public override void Update(TPayload prevPayload, TPayload payload, T item)
    {
        base.Update(prevPayload, payload, item);

        if ((prevPayload.AltId.IsDefault() && payload.AltId.IsDefault()) ||
            prevPayload.AltId.Equals(payload.AltId))
        {
            return;
        }

        if (!prevPayload.AltId.IsDefault())
        {
            _nodes.Remove(prevPayload.AltId);
        }

        if (!payload.AltId.IsDefault())
        {
            _nodes.Add(payload.AltId, item);
        }
    }

    public override void Clear()
    {
        _nodes.Clear();
        base.Clear();
    }
}
