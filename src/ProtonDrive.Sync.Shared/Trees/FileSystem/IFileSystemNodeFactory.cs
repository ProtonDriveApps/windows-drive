using System;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public interface IFileSystemNodeFactory<in TTree, TNode, in TModel, TId>
    where TTree : FileSystemTree<TTree, TNode, TModel, TId>
    where TNode : FileSystemNode<TTree, TNode, TModel, TId>
    where TModel : FileSystemNodeModel<TId>, new()
    where TId : IEquatable<TId>
{
    TNode CreateRootNode(TTree tree);
    TNode CreateNode(TTree tree, TModel model, TNode? parent);
}
