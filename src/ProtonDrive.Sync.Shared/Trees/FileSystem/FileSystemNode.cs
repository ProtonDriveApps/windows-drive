using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ProtonDrive.Shared;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public class FileSystemNode<TTree, TNode, TModel, TId> : FileSystemNodeBase, IIdentifiable<TId>
    where TTree : FileSystemTree<TTree, TNode, TModel, TId>
    where TNode : FileSystemNode<TTree, TNode, TModel, TId>
    where TModel : FileSystemNodeModel<TId>, new()
    where TId : IEquatable<TId>
{
    private TTree? _tree;
    private TModel _model;
    private IDictionary<TId, TNode>? _children;
    private IDictionary<string, IList<TNode>>? _childrenByName;

    protected internal FileSystemNode(
        TTree tree,
        TModel model,
        TNode? parent)
    {
        _tree = tree;
        _model = model;
        InternalParent = parent;
    }

    public TModel Model
    {
        get
        {
            ThrowIfDeleted();

            return _model;
        }
        internal set => _model = value;
    }

    public TId Id => _model.Id;
    public NodeType Type => _model.Type;
    public string Name => _model.Name;

    [MemberNotNullWhen(false, nameof(Parent))]
    public bool IsRoot => !IsDeleted && this == _tree.Root;

    public bool IsLeaf => Model.Type == NodeType.File || GetInitializedChildren().Count == 0;

    [MemberNotNullWhen(false, nameof(_tree))]
    public bool IsDeleted => _tree == null;

    public TNode? Parent
    {
        get
        {
            ThrowIfDeleted();

            return InternalParent ?? (IsRoot ? null : InternalParent = _tree.NodeByIdOrDefault(Model.ParentId));
        }
        internal set => InternalParent = value;
    }

    public IEnumerable<TNode> Children
    {
        get
        {
            foreach (var childId in GetInitializedChildren().Keys.ToList())
            {
                if (IsDeleted)
                {
                    yield break;
                }

                if (_children.TryGetValue(childId, out var node))
                {
                    yield return node;
                }
            }
        }
    }

    internal TNode? InternalParent { get; private set; }

    public IEnumerable<TNode> ChildrenByName(string name)
    {
        var childrenByName = GetChildrenByName();
        if (childrenByName == null)
        {
            return GetInitializedChildren().Values
                .Where(n => _tree.NameEqualityComparer.Equals(n.Name, name));
        }

        if (childrenByName.TryGetValue(name, out var children))
        {
            return children;
        }

        return Array.Empty<TNode>();
    }

    internal void AddChild(TNode child)
    {
        _children?.Add(child.Id, child);
        AddChildByName(child);
    }

    internal void OnChildRenamed(TNode child, string previousName)
    {
        RemoveChildByName(child, previousName);
        AddChildByName(child);
    }

    internal void RemoveChild(TNode child)
    {
        RemoveChildByName(child, child.Name);
        _children?.Remove(child.Id);
    }

    internal void Cleanup()
    {
        ClearChildren();
        Parent = null;
        _tree = null;
    }

    internal void ClearChildren()
    {
        _children?.Clear();
        _childrenByName?.Clear();
        _childrenByName = null;
    }

    [MemberNotNull(nameof(_tree), nameof(_children))]
    private IDictionary<TId, TNode> GetInitializedChildren()
    {
        ThrowIfDeleted();

        if (_children != null)
        {
            return _children;
        }

        var children = new SortedDictionary<TId, TNode>();
        foreach (var child in _tree.GetChildren((TNode)this))
        {
            children.Add(child.Id, child);
        }

        return _children = children;
    }

    private IDictionary<string, IList<TNode>>? GetChildrenByName()
    {
        ThrowIfDeleted();

        if (_childrenByName != null)
        {
            return _childrenByName;
        }

        var children = GetInitializedChildren();

        // It's not worth indexing by name small number of items
        if (children.Count < NumberOfChildrenToStartIndexingByName)
        {
            return null;
        }

        // Because of possible name duplicates the number of different names might be lower
        // than the total number of nodes. As having name duplicates is unlikely, the total
        // number of nodes is a good approximate.
        _childrenByName = new Dictionary<string, IList<TNode>>(children.Count, _tree.NameEqualityComparer);
        foreach (var child in children.Values)
        {
            AddChildByName(child);
        }

        return _childrenByName;
    }

    private void AddChildByName(TNode child)
    {
        if (_childrenByName == null)
        {
            return;
        }

        if (_childrenByName.TryGetValue(child.Name, out var nodesWithNewName))
        {
            nodesWithNewName.Add(child);
        }
        else
        {
            _childrenByName.Add(child.Name, new List<TNode>(1) { child });
        }
    }

    private void RemoveChildByName(TNode child, string name)
    {
        if (_childrenByName == null)
        {
            return;
        }

        if (!_childrenByName.TryGetValue(name, out var nodesWithPreviousName))
        {
            throw new TreeException("The child node with expected name does not exist");
        }

        nodesWithPreviousName.Remove(child);

        if (nodesWithPreviousName.Count != 0)
        {
            return;
        }

        _childrenByName.Remove(name);

        // It's not worth indexing by name small number of items
        if (_childrenByName.Count < NumberOfChildrenToStopIndexingByName)
        {
            _childrenByName.Clear();
            _childrenByName = null;
        }
    }

    [MemberNotNull(nameof(_tree))]
    private void ThrowIfDeleted()
    {
        if (IsDeleted)
        {
            throw TreeNodeDeletedException.FromNode(this);
        }
    }
}
