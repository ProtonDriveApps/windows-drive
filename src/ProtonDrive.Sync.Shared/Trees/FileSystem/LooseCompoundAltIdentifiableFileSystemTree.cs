using System;
using ProtonDrive.Sync.Shared.Trees.Collections;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

/// <summary>
/// Loosely compound alternatively identifiable file system tree. Tree node alternative identifier values are optional.
/// Multiple tree nodes with default AltId value can be added to the tree, they are excluded from node lookup
/// by AltID value. Other AltID values must be unique.
/// </summary>
/// <typeparam name="TTree">Type of the tree</typeparam>
/// <typeparam name="TNode">Type of the tree node</typeparam>
/// <typeparam name="TModel">Type of the tree node model</typeparam>
/// <typeparam name="TId">Type of the tree node identifier</typeparam>
/// <typeparam name="TAltId">Type of the tree node alternative identifier</typeparam>
public class LooseCompoundAltIdentifiableFileSystemTree<TTree, TNode, TModel, TId, TAltId> : FileSystemTree<TTree, TNode, TModel, TId>
    where TTree : FileSystemTree<TTree, TNode, TModel, TId>
    where TNode : FileSystemNode<TTree, TNode, TModel, TId>
    where TModel : LooseCompoundAltIdentifiableFileSystemNodeModel<TId, TAltId>, new()
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILooseCompoundAltIdentifiableTreeNodeRepository<TModel, TId, TAltId> _repository;
    private readonly LooseCompoundAltIdentifiableNodeDictionary<TNode, TModel, TId, TAltId> _nodes;

    public LooseCompoundAltIdentifiableFileSystemTree(
        ILooseCompoundAltIdentifiableTreeNodeRepository<TModel, TId, TAltId> repository,
        IFileSystemNodeFactory<TTree, TNode, TModel, TId> factory)
        : this(repository, factory, new LooseCompoundAltIdentifiableNodeDictionary<TNode, TModel, TId, TAltId>())
    {
    }

    private LooseCompoundAltIdentifiableFileSystemTree(
        ILooseCompoundAltIdentifiableTreeNodeRepository<TModel, TId, TAltId> repository,
        IFileSystemNodeFactory<TTree, TNode, TModel, TId> factory,
        LooseCompoundAltIdentifiableNodeDictionary<TNode, TModel, TId, TAltId> dictionary)
        : base(repository, factory, dictionary)
    {
        _repository = repository;
        _nodes = dictionary;
    }

    public TNode NodeByAltId(LooseCompoundAltIdentity<TAltId> altId)
    {
        return NodeByAltIdOrDefault(altId) ?? throw new TreeException($"Tree node with AltId={altId} does not exist");
    }

    public TNode? NodeByAltIdOrDefault(LooseCompoundAltIdentity<TAltId> altId)
    {
        InitRoot();

        if (_nodes.TryGetByAltId(altId, out var node))
        {
            return node;
        }

        var model = _repository.NodeByAltId(altId);

        return model == null ? null : NewNode(model, null);
    }
}
