using System;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public static class FileSystemNodeModelExtensions
{
    public static TModel CopiedFrom<TModel, TId>(this TModel model, IFileSystemNodeModel<TId> other)
        where TModel : FileSystemNodeModel<TId>
        where TId : IEquatable<TId>
    {
        model.CopyFrom(other);

        return model;
    }

    public static TModel WithLinkFrom<TModel, TId>(this TModel model, IFileSystemNodeModel<TId> other)
        where TModel : FileSystemNodeModel<TId>
        where TId : IEquatable<TId>
    {
        model.CopyLinkFrom(other);

        return model;
    }

    public static TModel WithAttributesFrom<TModel, TId>(this TModel model, IFileSystemNodeModel<TId> other)
        where TModel : FileSystemNodeModel<TId>
        where TId : IEquatable<TId>
    {
        model.CopyAttributesFrom(other);

        return model;
    }

    public static TModel WithMetadataFrom<TModel, TId>(this TModel model, IFileSystemNodeModel<TId> other)
        where TModel : FileSystemNodeModel<TId>
        where TId : IEquatable<TId>
    {
        model.CopyMetadataFrom(other);

        return model;
    }

    public static TModel WithType<TModel, TId>(this TModel model, NodeType value)
        where TModel : FileSystemNodeModel<TId>
        where TId : IEquatable<TId>
    {
        model.Type = value;

        return model;
    }

    public static TModel WithId<TModel, TId>(this TModel model, TId value)
        where TModel : FileSystemNodeModel<TId>
        where TId : IEquatable<TId>
    {
        model.Id = value;

        return model;
    }

    public static TModel WithParentId<TModel, TId>(this TModel model, TId parentId)
        where TModel : FileSystemNodeModel<TId>
        where TId : IEquatable<TId>
    {
        model.ParentId = parentId;

        return model;
    }

    public static TModel WithName<TModel, TId>(this TModel model, string value)
        where TModel : FileSystemNodeModel<TId>
        where TId : IEquatable<TId>
    {
        model.Name = value;

        return model;
    }

    public static TModel WithContentVersion<TModel, TId>(this TModel model, long value)
        where TModel : FileSystemNodeModel<TId>
        where TId : IEquatable<TId>
    {
        model.ContentVersion = value;

        return model;
    }

    public static void CopyFrom<TId>(this FileSystemNodeModel<TId> model, IFileSystemNodeModel<TId> other)
        where TId : IEquatable<TId>
    {
        model.Id = other.Id;
        CopyLinkFrom(model, other);
        CopyAttributesFrom(model, other);
        model.CopyMetadataFrom(other);
    }

    public static void CopyLinkFrom<TId>(this FileSystemNodeModel<TId> model, IFileSystemNodeModel<TId> other)
        where TId : IEquatable<TId>
    {
        model.ParentId = other.ParentId;
        model.Name = other.Name;
    }

    public static void CopyAttributesFrom<TId>(this FileSystemNodeModel<TId> model, IFileSystemNodeModel<TId> other)
        where TId : IEquatable<TId>
    {
        model.Type = other.Type;
        model.ContentVersion = other.ContentVersion;
    }
}
