using System;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public interface IFileSystemNodeModel<TId> : IIdentifiableTreeNode<TId>
    where TId : IEquatable<TId>
{
    NodeType Type { get; }
    string Name { get; }
    long ContentVersion { get; }
}
