using System;
using System.Collections.Generic;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.Trees.Collections;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

/// <summary>
/// The tree of named identifiable file system nodes.
/// </summary>
/// <typeparam name="TTree">Type of the tree</typeparam>
/// <typeparam name="TNode">Type of the tree node</typeparam>
/// <typeparam name="TModel">Type of the tree node model</typeparam>
/// <typeparam name="TId">Type of the tree node identifier</typeparam>
public class FileSystemTree<TTree, TNode, TModel, TId>
    where TTree : FileSystemTree<TTree, TNode, TModel, TId>
    where TNode : FileSystemNode<TTree, TNode, TModel, TId>
    where TModel : FileSystemNodeModel<TId>, new()
    where TId : IEquatable<TId>
{
    private readonly ITreeNodeRepository<TModel, TId> _repository;
    private readonly IFileSystemNodeFactory<TTree, TNode, TModel, TId> _factory;
    private readonly IdentifiableNodeDictionary<TNode, TModel, TId> _allNodes;

    private TNode? _root;

    public FileSystemTree(
        ITreeNodeRepository<TModel, TId> repository,
        IFileSystemNodeFactory<TTree, TNode, TModel, TId> factory,
        IdentifiableNodeDictionary<TNode, TModel, TId> dictionary)
    {
        _repository = repository;
        _factory = factory;
        _allNodes = dictionary;

        Operations = new FileSystemTreeOperations<TTree, TNode, TModel, TId>((TTree)this);
    }

    public IEqualityComparer<string> NameEqualityComparer { get; } = StringComparer.OrdinalIgnoreCase;

    public FileSystemTreeOperations<TTree, TNode, TModel, TId> Operations { get; }

    public TNode Root
    {
        get
        {
            return _root ?? InitRoot();
        }
    }

    public TNode NodeById(TId id)
    {
        return NodeByIdOrDefault(id) ?? throw new TreeException($"Node with Id={id} does not exist in the tree");
    }

    public TNode? NodeByIdOrDefault(TId? id) => GetNodeById(id);

    public TNode DirectoryById(TId id)
    {
        var node = NodeById(id);
        if (node.Type != NodeType.Directory)
        {
            throw new TreeException($"Node with Id={id} is not a directory");
        }

        return node;
    }

    public TNode FileById(TId id)
    {
        var node = NodeById(id);
        if (node.Type != NodeType.File)
        {
            throw new TreeException($"Node with Id={id} is not a file");
        }

        return node;
    }

    public void Clear()
    {
        _root?.ClearChildren();
        _root = null;

        foreach (var node in _allNodes)
        {
            node.Cleanup();
        }

        _allNodes.Clear();

        _repository.Clear();
    }

    public TNode Create(TModel model)
    {
        Ensure.NotNull(model.ParentId, nameof(model), nameof(model.ParentId));

        if (model.ParentId.Equals(model.Id))
        {
            throw new TreeException($"Cannot create node with ParentId=Id={model.Id}");
        }

        return CreateNode(DirectoryById(model.ParentId), model);
    }

    public void Edit(TNode node, long version)
    {
        if (node == Root)
        {
            throw new TreeException("Cannot edit the root node");
        }

        var prevModel = node.Model;

        // The model held by the node should not be modified
        node.Model = node.Model.Copy()
            .WithContentVersion<TModel, TId>(version);

        _allNodes.Update(prevModel, node.Model, node);
        _repository.Update(node.Model);
    }

    public void Update(TNode node, TModel model)
    {
        /* The root node is allowed to be updated */

        var prevModel = node.Model;

        // The model held by the node should not be modified
        node.Model = node.Model.Copy()
            .WithMetadataFrom(model);

        _allNodes.Update(prevModel, node.Model, node);
        _repository.Update(node.Model);
    }

    public void Rename(TNode node, string name)
    {
        Ensure.NotNullOrEmpty(name, nameof(name));

        if (node == Root)
        {
            throw new TreeException("Cannot rename the root node");
        }

        var previousName = node.Model.Name;

        // The model held by the node should not be modified
        node.Model = node.Model.Copy()
            .WithName<TModel, TId>(name);

        node.InternalParent?.OnChildRenamed(node, previousName);

        _repository.Update(node.Model);
    }

    public void Move(TNode node, TNode parent, string name)
    {
        Ensure.NotNullOrEmpty(name, nameof(name));
        Ensure.NotNull(node.Model.ParentId, nameof(node), $"{nameof(node.Model)}.{nameof(node.Model.ParentId)}");

        if (node == Root)
        {
            throw new TreeException("Cannot move the root node");
        }

        if (parent.Id.Equals(node.Id))
        {
            throw new TreeException($"Moving node to ParentId=Id={node.Id} creates a circular dependency");
        }

        CheckForCircularDependency(node, parent);

        var previousParent = node.InternalParent;
        if (previousParent == null && _allNodes.TryGet(node.Model.ParentId, out var parentNode))
        {
            previousParent = parentNode;
        }

        var isMove = parent != previousParent;
        if (isMove)
        {
            previousParent?.RemoveChild(node);
        }

        var previousName = node.Model.Name;

        // The model held by the node should not be modified
        node.Model = node.Model.Copy()
            .WithName<TModel, TId>(name)
            .WithParentId(parent.Id);

        if (isMove)
        {
            node.Parent = parent;
            parent.AddChild(node);
        }
        else
        {
            parent.OnChildRenamed(node, previousName);
        }

        _repository.Update(node.Model);
    }

    public void Delete(TNode node)
    {
        if (node == Root)
        {
            throw new TreeException("Cannot delete the root node");
        }

        var nodeIsLeaf = true;

        foreach (var descendant in DescendantIdsInPostOrder(node.Id))
        {
            nodeIsLeaf = false;

            if (!descendant.IsLeaf)
            {
                _repository.DeleteChildren(descendant.Id);
            }

            if (!_allNodes.TryGet(descendant.Id, out var descendantNode))
            {
                continue;
            }

            _allNodes.Remove(descendantNode.Model);
            descendantNode.Cleanup();
        }

        if (!nodeIsLeaf)
        {
            _repository.DeleteChildren(node.Id);
        }

        var nodeModel = node.Model;
        _repository.Delete(nodeModel);

        if (_allNodes.TryGet(nodeModel.ParentId, out var parent))
        {
            parent.RemoveChild(node);
        }

        _allNodes.Remove(nodeModel);
        node.Cleanup();

        IEnumerable<(bool IsLeaf, TId Id)> DescendantIdsInPostOrder(TId nodeId)
        {
            foreach (var childId in _repository.ChildrenIds(nodeId))
            {
                var isLeaf = true;

                foreach (var subChildId in DescendantIdsInPostOrder(childId))
                {
                    yield return subChildId;

                    isLeaf = false;
                }

                yield return (isLeaf, childId);
            }
        }
    }

    internal IEnumerable<TNode> GetChildren(TNode parent)
    {
        if (parent.Type != NodeType.Directory)
        {
            throw new TreeException($"Node with Id={parent.Id} is not a directory");
        }

        foreach (var model in _repository.Children(parent.Model))
        {
            if (_allNodes.TryGet(model.Id, out var node))
            {
                node.Parent = parent;
            }
            else
            {
                node = NewNode(model, parent);
            }

            yield return node;
        }
    }

    protected TNode InitRoot()
    {
        if (_root != null)
        {
            return _root;
        }

        var root = _factory.CreateRootNode((TTree)this);
        var rootModel = _repository.NodeById(root.Id);
        if (rootModel == null)
        {
            _repository.Create(root.Model);
        }
        else
        {
            root = _factory.CreateNode((TTree)this, rootModel, default);
        }

        _allNodes.Add(root.Model, root);
        _root = root;

        return _root;
    }

    protected TNode NewNode(TModel model, TNode? parent)
    {
        var node = _factory.CreateNode((TTree)this, model, parent);
        _allNodes.Add(model, node);

        return node;
    }

    private TNode CreateNode(TNode parent, TModel nodeModel)
    {
        var model = nodeModel.Copy();
        _repository.Create(model);

        var node = NewNode(model, parent);
        parent.AddChild(node);

        return node;
    }

    private TNode? GetNodeById(TId? id)
    {
        Ensure.NotNull(id, nameof(id));

        InitRoot();

        if (_allNodes.TryGet(id, out var node))
        {
            return node;
        }

        var model = _repository.NodeById(id);
        if (model == null)
        {
            return null;
        }

        return NewNode(model, null);
    }

    private void CheckForCircularDependency(TNode node, TNode newParent)
    {
        /* Moving the node into one of its own children breaks connection with the root and creates a circular dependency.*/

        if (newParent.IsRoot)
        {
            return;
        }

        if (newParent.Id.Equals(node.Model.ParentId))
        {
            return;
        }

        var parent = newParent.Parent;
        while (parent?.IsRoot == false)
        {
            if (parent == node)
            {
                throw new TreeException($"Moving the node with Id={node.Id} from parent node with Id={node.Model.ParentId} to Id={newParent.Id} creates a circular dependency");
            }

            parent = parent.Parent;
        }
    }
}
