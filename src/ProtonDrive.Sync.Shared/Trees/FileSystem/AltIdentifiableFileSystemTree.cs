using System;
using ProtonDrive.Sync.Shared.Trees.Collections;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

/// <summary>
/// Alternatively identifiable file system tree. Tree nodes must have unique AltID values.
/// </summary>
/// <typeparam name="TTree">Type of the tree</typeparam>
/// <typeparam name="TNode">Type of the tree node</typeparam>
/// <typeparam name="TModel">Type of the tree node model</typeparam>
/// <typeparam name="TId">Type of the tree node identifier</typeparam>
/// <typeparam name="TAltId">Type of the tree node alternative identifier</typeparam>
public class AltIdentifiableFileSystemTree<TTree, TNode, TModel, TId, TAltId> : FileSystemTree<TTree, TNode, TModel, TId>
    where TTree : FileSystemTree<TTree, TNode, TModel, TId>
    where TNode : FileSystemNode<TTree, TNode, TModel, TId>
    where TModel : FileSystemNodeModel<TId>, IAltIdentifiable<TId, TAltId>, new()
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly IAltIdentifiableTreeNodeRepository<TModel, TId, TAltId> _repository;
    private readonly AltIdentifiableNodeDictionary<TNode, TModel, TId, TAltId> _nodes;

    public AltIdentifiableFileSystemTree(
        IAltIdentifiableTreeNodeRepository<TModel, TId, TAltId> repository,
        IFileSystemNodeFactory<TTree, TNode, TModel, TId> factory)
        : this(repository, factory, new AltIdentifiableNodeDictionary<TNode, TModel, TId, TAltId>())
    {
    }

    private AltIdentifiableFileSystemTree(
        IAltIdentifiableTreeNodeRepository<TModel, TId, TAltId> repository,
        IFileSystemNodeFactory<TTree, TNode, TModel, TId> factory,
        AltIdentifiableNodeDictionary<TNode, TModel, TId, TAltId> dictionary)
        : base(repository, factory, dictionary)
    {
        _repository = repository;
        _nodes = dictionary;
    }

    public TNode NodeByAltId(TAltId altId)
    {
        return NodeByAltIdOrDefault(altId) ?? throw new TreeException($"Node with AltId={altId} does not exist in the tree");
    }

    public TNode? NodeByAltIdOrDefault(TAltId altId)
    {
        InitRoot();

        if (_nodes.TryGetByAltId(altId, out var node))
        {
            return node;
        }

        var model = _repository.NodeByAltId(altId);
        if (model == null)
        {
            return null;
        }

        return NewNode(model, null);
    }
}
