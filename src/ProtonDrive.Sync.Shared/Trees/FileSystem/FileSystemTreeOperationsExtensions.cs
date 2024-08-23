using System;
using System.Collections.Generic;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public static class FileSystemTreeOperationsExtensions
{
    public static void Execute<TTree, TNode, TModel, TId>(
        this FileSystemTreeOperations<TTree, TNode, TModel, TId> subject,
        IEnumerable<Operation<TModel>> operations)
        where TTree : FileSystemTree<TTree, TNode, TModel, TId>
        where TNode : FileSystemNode<TTree, TNode, TModel, TId>
        where TModel : FileSystemNodeModel<TId>, new()
        where TId : IEquatable<TId>
    {
        foreach (var operation in operations)
        {
            subject.Execute(operation);
        }
    }
}
