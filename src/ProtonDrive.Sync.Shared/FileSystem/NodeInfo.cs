using System;
using System.IO;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;

namespace ProtonDrive.Sync.Shared.FileSystem;

public class NodeInfo<TId> : ICloneable
    where TId : IEquatable<TId>
{
    public TId? Id { get; private set; }
    public string? RevisionId { get; private set; }
    public TId? ParentId { get; private set; }
    public RootInfo<TId>? Root { get; private set; }
    public string Path { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public FileAttributes Attributes { get; private set; }
    public PlaceholderState PlaceholderState { get; private set; }
    public DateTime LastWriteTimeUtc { get; private set; }
    public long Size { get; private set; } = -1;
    public long? SizeOnStorage { get; private set; }

    public bool IsEmpty => Root is null && (Id is null || Id.Equals(default)) && string.IsNullOrEmpty(Path) && string.IsNullOrEmpty(Name);

    public static NodeInfo<TId> Empty() => new();
    public static NodeInfo<TId> File() => new();
    public static NodeInfo<TId> Directory() => new NodeInfo<TId>().WithAttributes(FileAttributes.Directory).WithSize(0);

    object ICloneable.Clone()
    {
        return ((NodeInfo<TId>)MemberwiseClone()).WithRoot(Root?.Copy());
    }

    public NodeInfo<TId> WithId(TId? value)
    {
        Id = value;

        return this;
    }

    public NodeInfo<TId> WithRevisionId(string? value)
    {
        RevisionId = value;

        return this;
    }

    public NodeInfo<TId> WithParentId(TId? value)
    {
        ParentId = value;

        return this;
    }

    public NodeInfo<TId> WithRoot(RootInfo<TId>? value)
    {
        Root = value;

        return this;
    }

    public NodeInfo<TId> WithPath(string value)
    {
        Path = value;

        return this;
    }

    public NodeInfo<TId> WithName(string value)
    {
        Name = value;

        return this;
    }

    public NodeInfo<TId> WithAttributes(FileAttributes value)
    {
        Attributes = value;

        return this;
    }

    public NodeInfo<TId> WithLastWriteTimeUtc(DateTime value)
    {
        LastWriteTimeUtc = value;

        return this;
    }

    public NodeInfo<TId> WithSize(long? value)
    {
        Size = value ?? -1;

        return this;
    }

    public NodeInfo<TId> WithSizeOnStorage(long value)
    {
        SizeOnStorage = value;

        return this;
    }

    public NodeInfo<TId> WithPlaceholderState(PlaceholderState value)
    {
        PlaceholderState = value;

        return this;
    }
}
