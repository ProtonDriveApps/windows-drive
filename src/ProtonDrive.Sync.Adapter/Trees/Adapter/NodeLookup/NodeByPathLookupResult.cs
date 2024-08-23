using System;

namespace ProtonDrive.Sync.Adapter.Trees.Adapter.NodeLookup;

internal class NodeByPathLookupResult<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly AdapterTreeNode<TId, TAltId>? _value;

    public AdapterTreeNode<TId, TAltId> Value
    {
        get => _value ?? throw new ArgumentNullException(nameof(Value));
        init => _value = value;
    }

    public bool IsAncestor { get; init; }
    public bool IsParent { get; init; }
    public string? MissingChildName { get; init; }

    public static NodeByPathLookupResult<TId, TAltId> NodeFound(AdapterTreeNode<TId, TAltId> value)
    {
        return new()
        {
            Value = value,
            IsAncestor = false,
            IsParent = false,
            MissingChildName = null,
        };
    }

    public static NodeByPathLookupResult<TId, TAltId> ParentFound(AdapterTreeNode<TId, TAltId> value, string missingChildName)
    {
        return new()
        {
            Value = value,
            IsAncestor = true,
            IsParent = true,
            MissingChildName = missingChildName,
        };
    }

    public static NodeByPathLookupResult<TId, TAltId> AncestorFound(AdapterTreeNode<TId, TAltId> value, string? missingChildName)
    {
        return new()
        {
            Value = value,
            IsAncestor = true,
            IsParent = false,
            MissingChildName = missingChildName,
        };
    }
}
