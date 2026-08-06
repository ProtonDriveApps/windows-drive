using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;
using Proton.Drive.Shared;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;

internal class UpdateLogging<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<NodeUpdateDetection<TId, TAltId>> _logger;
    private readonly Replica _replica;
    private readonly AdapterTree<TId, TAltId> _adapterTree;

    private readonly OperationLogging<TId> _operationLogging;

    public UpdateLogging(ILogger<NodeUpdateDetection<TId, TAltId>> logger, Replica replica, AdapterTree<TId, TAltId> adapterTree)
    {
        _logger = logger;
        _replica = replica;
        _adapterTree = adapterTree;

        _operationLogging = new OperationLogging<TId>($"Adding {replica} Detected Update", logger);
    }

    public void LogDetectedOperation(
        Operation<AdapterTreeNodeModel<TId, TAltId>> operation,
        AdapterTreeNodeModel<TId, TAltId>? previous)
    {
        /* Operation is already executed on the Adapter Tree */

        var model = operation.Model;

        switch (operation.Type)
        {
            case OperationType.Create:
                LogCreation(operation, model);
                break;

            case OperationType.Edit:
                LogEdit(operation, model);
                break;

            case OperationType.Move:
                LogMove(operation, previous, model);
                break;

            case OperationType.Delete:
                LodDeletion(operation, previous);
                break;

            default:
                throw new InvalidEnumArgumentException();
        }
    }

    public void LogDetectedUpdate(Operation<FileSystemNodeModel<TId>> operation)
    {
        _operationLogging.LogOperation(operation);
    }

    private void LogCreation(Operation<AdapterTreeNodeModel<TId, TAltId>> operation, AdapterTreeNodeModel<TId, TAltId> model)
    {
        var node = GetNodeById(model.Id);
        var (root, path) = node.Path();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            if (model.Type is NodeType.File)
            {
                _logger.LogInformation(
                    "Detected {Replica} {OperationType} {Type} \"{Root}\"/\"{Path}\"/{Id} {AltId} at parent {ParentId} {ParentAltId}, ContentVersion={ContentVersion}, LastWriteTime={LastWriteTime:O}, Size={Size}",
                    _replica,
                    operation.Type,
                    model.Type,
                    root,
                    path,
                    node.Id,
                    node.AltId,
                    node.Parent!.Id,
                    node.Parent.AltId,
                    model.ContentVersion,
                    model.LastWriteTime,
                    model.Size);
            }
            else
            {
                _logger.LogInformation(
                    "Detected {Replica} {OperationType} {Type} \"{Root}\"/\"{Path}\"/{Id} {AltId} at parent {ParentId} {ParentAltId}",
                    _replica,
                    operation.Type,
                    model.Type,
                    root,
                    path,
                    node.Id,
                    node.AltId,
                    node.Parent!.Id,
                    node.Parent.AltId);
            }
        }
        else
        {
            if (model.Type is NodeType.File)
            {
                _logger.LogInformation(
                    "Detected {Replica} {OperationType} {Type} \"{Root}\"/{Id} {AltId} at parent {ParentId} {ParentAltId}, ContentVersion={ContentVersion}, Size={Size} KiB",
                    _replica,
                    operation.Type,
                    model.Type,
                    root,
                    node.Id,
                    node.AltId,
                    node.Parent!.Id,
                    node.Parent.AltId,
                    model.ContentVersion,
                    (model.Size + 1023) / 1024);
            }
            else
            {
                _logger.LogInformation(
                    "Detected {Replica} {OperationType} {Type} \"{Root}\"/{Id} {AltId} at parent {ParentId} {ParentAltId}",
                    _replica,
                    operation.Type,
                    model.Type,
                    root,
                    node.Id,
                    node.AltId,
                    node.Parent!.Id,
                    node.Parent.AltId);
            }
        }
    }

    private void LogEdit(Operation<AdapterTreeNodeModel<TId, TAltId>> operation, AdapterTreeNodeModel<TId, TAltId> model)
    {
        var node = GetNodeById(model.Id);
        var (root, path) = node.Path();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogInformation(
                "Detected {OperationType} {Type} \"{Root}\"/\"{Path}\"/{Id} {AltId}, ContentVersion={ContentVersion}, LastWriteTime={LastWriteTime:O}, Size={Size}",
                operation.Type,
                node.Type,
                root,
                path,
                node.Id,
                node.AltId,
                model.ContentVersion,
                model.LastWriteTime,
                model.Size);
        }
        else
        {
            _logger.LogInformation(
                "Detected {OperationType} {Type} \"{Root}\"/{Id} {AltId}, ContentVersion={ContentVersion}, Size={Size} KiB",
                operation.Type,
                node.Type,
                root,
                node.Id,
                node.AltId,
                model.ContentVersion,
                (model.Size + 1023) / 1024);
        }
    }

    private void LogMove(
        Operation<AdapterTreeNodeModel<TId, TAltId>> operation,
        AdapterTreeNodeModel<TId, TAltId>? previous,
        AdapterTreeNodeModel<TId, TAltId> model)
    {
        var node = GetNodeById(model.Id);
        Ensure.NotNull(previous, nameof(previous));
        var oldParent = GetNodeById(previous.ParentId);
        var (oldRoot, oldParentPath) = oldParent.Path();
        var oldPath = Path.Combine(oldParentPath, previous.Name);
        var isRename = model.ParentId.Equals(previous.ParentId);

        if (isRename)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogInformation(
                    "Detected {OperationType} {Type} \"{Root}\"/\"{Path}\"/{Id} {AltId} to \"{NewName}\"",
                    "Rename",
                    node.Type,
                    oldRoot,
                    oldPath,
                    node.Id,
                    node.AltId,
                    model.Name);
            }
            else
            {
                _logger.LogInformation(
                    "Detected {OperationType} {Type} \"{Root}\"/{Id} {AltId}",
                    "Rename",
                    node.Type,
                    oldRoot,
                    node.Id,
                    node.AltId);
            }
        }
        else
        {
            var (newRoot, newParentPath) = node.Parent!.Path();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogInformation(
                    "Detected {OperationType} {Type} \"{Root}\"/\"{Path}\"/{Id} {AltId} to parent \"{NewRoot}\"/\"{NewPath}\"/{ParentId} {ParentAltId} as \"{NewName}\" from {PreviousParentId}",
                    operation.Type,
                    node.Type,
                    oldRoot,
                    oldPath,
                    node.Id,
                    node.AltId,
                    newRoot,
                    newParentPath,
                    node.Parent!.Id,
                    node.Parent.AltId,
                    model.Name,
                    previous.ParentId);
            }
            else
            {
                _logger.LogInformation(
                    "Detected {OperationType} {Type} \"{Root}\"/{Id} {AltId} to parent \"{NewRoot}\"/{ParentId} {ParentAltId} from {PreviousParentId}",
                    operation.Type,
                    node.Type,
                    oldRoot,
                    node.Id,
                    node.AltId,
                    newRoot,
                    node.Parent!.Id,
                    node.Parent.AltId,
                    previous.ParentId);
            }
        }
    }

    private void LodDeletion(Operation<AdapterTreeNodeModel<TId, TAltId>> operation, AdapterTreeNodeModel<TId, TAltId>? previous)
    {
        Ensure.NotNull(previous, nameof(previous));
        var parent = GetNodeById(previous.ParentId);
        var (root, parentPath) = parent.Path();
        var path = Path.Combine(parentPath, previous.Name);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogInformation(
                "Detected {OperationType} {Type} \"{Root}\"/\"{Path}\"/{Id} {AltId} at parent {ParentId} {ParentAltId}",
                operation.Type,
                previous.Type,
                root,
                path,
                previous.Id,
                previous.AltId,
                parent.Id,
                parent.AltId);
        }
        else
        {
            _logger.LogInformation(
                "Detected {OperationType} {Type} \"{Root}\"/{Id} {AltId} at parent {ParentId} {ParentAltId}",
                operation.Type,
                previous.Type,
                root,
                previous.Id,
                previous.AltId,
                parent.Id,
                parent.AltId);
        }
    }

    private AdapterTreeNode<TId, TAltId> GetNodeById(TId id)
    {
        return _adapterTree.NodeById(id);
    }
}
