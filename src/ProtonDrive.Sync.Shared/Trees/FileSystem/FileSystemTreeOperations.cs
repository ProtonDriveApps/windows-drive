using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public class FileSystemTreeOperations<TTree, TNode, TModel, TId> : IEnumerable<Operation<TModel>>
    where TTree : FileSystemTree<TTree, TNode, TModel, TId>
    where TNode : FileSystemNode<TTree, TNode, TModel, TId>
    where TModel : FileSystemNodeModel<TId>, new()
    where TId : IEquatable<TId>
{
    private readonly TTree _tree;
    private readonly PassiveTreeTraversal<TTree, TNode, TModel, TId> _treeTraversal = new();

    public FileSystemTreeOperations(TTree tree)
    {
        _tree = tree;
    }

    public event EventHandler<FileSystemTreeOperationExecutingEventArgs<TModel, TId>>? Executing;
    public event EventHandler<FileSystemTreeOperationExecutedEventArgs<TModel, TId>>? Executed;

    public void Execute(Operation<TModel> operation)
    {
        var node = _tree.NodeByIdOrDefault(operation.Model.Id);
        var oldNodeModel = node?.Model;
        var newNodeModel = operation.Model;

        switch (operation.Type)
        {
            case OperationType.Create:
                ShouldNotExist(node);
                newNodeModel = OnExecuting(operation.Type, newNodeModel, oldNodeModel);
                node = Create(newNodeModel);
                break;

            case OperationType.Update:
                ShouldExist(node, newNodeModel.Id);
                newNodeModel = OnExecuting(operation.Type, newNodeModel, oldNodeModel);
                Update(node, newNodeModel);
                break;

            case OperationType.Edit:
                ShouldExist(node, newNodeModel.Id);
                newNodeModel = OnExecuting(operation.Type, newNodeModel, oldNodeModel);
                Edit(node, newNodeModel);
                break;

            case OperationType.Move:
                ShouldExist(node, newNodeModel.Id);
                newNodeModel = OnExecuting(operation.Type, newNodeModel, oldNodeModel);
                Move(node, newNodeModel);
                break;

            case OperationType.Delete:
                ShouldExist(node, newNodeModel.Id);
                OnExecuting(operation.Type, newNodeModel: null, oldNodeModel);
                Delete(node);
                node = null;
                break;

            default:
                throw new InvalidOperationException();
        }

        OnExecuted(operation.Type, node?.Model, oldNodeModel);
    }

    public IEnumerator<Operation<TModel>> GetEnumerator() =>
        _treeTraversal
            .ExcludeStartingNode()
            .PreOrder(_tree.Root, CancellationToken.None)
            .Select(CreateOperation)
            .GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private Operation<TModel> CreateOperation(TNode node)
    {
        return new Operation<TModel>(OperationType.Create, node.Model);
    }

    private TNode Create(TModel model)
    {
        return _tree.Create(model);
    }

    private void Edit(TNode node, TModel model)
    {
        _tree.Edit(node, model.ContentVersion);
        _tree.Update(node, model);
    }

    private void Update(TNode node, TModel model)
    {
        _tree.Update(node, model);
    }

    private void Move(TNode node, TModel model)
    {
        _tree.Update(node, model);

        if (model.ParentId.Equals(node.Model.ParentId))
        {
            _tree.Rename(node, model.Name);
        }
        else
        {
            var parent = _tree.DirectoryById(model.ParentId);
            _tree.Move(node, parent, model.Name);
        }
    }

    private void Delete(TNode node)
    {
        _tree.Delete(node);
    }

    [return: NotNullIfNotNull("newNodeModel")]
    private TModel? OnExecuting(OperationType operationType, TModel? newNodeModel, TModel? oldNodeModel)
    {
        var handler = Executing;
        if (handler is null)
        {
            return newNodeModel;
        }

        newNodeModel = operationType switch
        {
            OperationType.Create => newNodeModel,
            OperationType.Edit => newNodeModel?.Copy().WithLinkFrom(oldNodeModel!),
            OperationType.Move => newNodeModel?.Copy().WithAttributesFrom(oldNodeModel!),
            OperationType.Delete => newNodeModel,
            OperationType.Update => newNodeModel?.Copy().WithLinkFrom(oldNodeModel!).WithAttributesFrom(oldNodeModel!),
            _ => throw new InvalidEnumArgumentException(nameof(operationType), (int)operationType, typeof(OperationType)),
        };

        var eventArgs = new FileSystemTreeOperationExecutingEventArgs<TModel, TId>(operationType, oldNodeModel, newNodeModel);

        handler.Invoke(this, eventArgs);

        return eventArgs.NewModel;
    }

    private void OnExecuted(OperationType operationType, TModel? newNodeModel, TModel? oldNodeModel)
    {
        Executed?.Invoke(this, new FileSystemTreeOperationExecutedEventArgs<TModel, TId>(operationType, oldNodeModel, newNodeModel));
    }

    private void ShouldNotExist(TNode? node)
    {
        if (node != null)
        {
            throw new TreeException($"Tree node with Id={node.Id} already exists");
        }
    }

    private void ShouldExist([NotNull] TNode? node, TId id)
    {
        if (node == null)
        {
            throw new TreeException($"Tree node with Id={id} does not exist");
        }
    }
}
