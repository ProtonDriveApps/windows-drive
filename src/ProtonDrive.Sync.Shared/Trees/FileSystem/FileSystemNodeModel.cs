using System;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public class FileSystemNodeModel<TId> : IFileSystemNodeModel<TId>, ICloneable
    where TId : IEquatable<TId>
{
    private TId? _id;
    private TId? _parentId;

    public TId Id
    {
        get => _id ?? throw new ArgumentNullException(nameof(Id));
        set => _id = value;
    }

    // Link
    public TId ParentId
    {
        get => _parentId ?? throw new ArgumentNullException(nameof(ParentId));
        set => _parentId = value;
    }

    public string Name { get; set; } = string.Empty;

    // Attributes
    public NodeType Type { get; set; }
    public long ContentVersion { get; set; }

    public object Clone() => MemberwiseClone();

    public virtual void CopyMetadataFrom(IFileSystemNodeModel<TId> other)
    {
    }
}
