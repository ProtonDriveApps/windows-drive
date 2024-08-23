using System;
using System.Collections.Generic;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Shared.Trees;

public class LeavesRemovalOperationsFactory<TTree, TNode, TModel, TId>
    where TTree : FileSystemTree<TTree, TNode, TModel, TId>
    where TNode : FileSystemNode<TTree, TNode, TModel, TId>
    where TModel : FileSystemNodeModel<TId>, new()
    where TId : IEquatable<TId>
{
    private readonly Predicate<TModel> _predicate;

    /// <summary>
    /// Creates file system tree leaves removal operations factory.
    /// </summary>
    /// <param name="predicate">The method that determines whether the specified node should be deleted if it is a leaf.</param>
    public LeavesRemovalOperationsFactory(Predicate<TModel> predicate)
    {
        _predicate = predicate;
    }

    /// <summary>
    /// Generates a sequence of leaf nodes deletion operations starting from the specified node
    /// and moving up into ancestors.
    /// </summary>
    /// <remarks>
    /// The current operation should be executed before accessing the next one so that the next
    /// node become the leaf before generating the operation for it.
    /// </remarks>
    /// <param name="node">The node to start leaf deletion from.</param>
    /// <returns>A sequence of leaf nodes deletion operations.</returns>
    public IEnumerable<Operation<TModel>> Operations(TNode? node)
    {
        if (node is null || node.IsDeleted)
        {
            yield break;
        }

        while (!node.IsRoot && node.IsLeaf && _predicate(node.Model))
        {
            var parent = node.Parent;

            yield return new Operation<TModel>(
                OperationType.Delete,
                node.Model.Copy());

            node = parent!;
        }
    }
}
