using System;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.Trees.Adapter;

internal static class AdapterTreeOperationsExtensions
{
    public static void LogAndExecute<TId, TAltId>(
        this FileSystemTreeOperations<AdapterTree<TId, TAltId>, AdapterTreeNode<TId, TAltId>, AdapterTreeNodeModel<TId, TAltId>, TId> subject,
        ILogger logger,
        Operation<AdapterTreeNodeModel<TId, TAltId>> operation)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        LogTreeOperation(logger, operation);

        subject.Execute(operation);
    }

    private static void LogTreeOperation<TId, TAltId>(ILogger logger, Operation<AdapterTreeNodeModel<TId, TAltId>> operation)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        var model = operation.Model;

        switch (operation.Type)
        {
            case OperationType.Create:
                logger.LogDebug(
                    "Executing on Adapter Tree: {OperationType} {Type} \"{Name}\" Id={ParentId}/{Id} {AltId}, LastWriteTime={LastWriteTime:O}, Size={Size}, ContentVersion={ContentVersion}, Status={Status}",
                    operation.Type,
                    model.Type,
                    model.Name,
                    model.ParentId,
                    model.Id,
                    model.AltId,
                    model.LastWriteTime,
                    model.Size,
                    model.ContentVersion,
                    model.Status);
                break;

            case OperationType.Edit:
                logger.LogDebug(
                    "Executing on Adapter Tree: {OperationType} Id={Id} LastWriteTime={LastWriteTime:O}, Size={Size}, ContentVersion={ContentVersion}",
                    operation.Type,
                    model.Id,
                    model.LastWriteTime,
                    model.Size,
                    model.ContentVersion);
                break;

            case OperationType.Move:
                logger.LogDebug(
                    "Executing on Adapter Tree: {OperationType} \"{Name}\" Id={ParentId}/{Id}",
                    operation.Type,
                    model.Name,
                    model.ParentId,
                    model.Id);
                break;

            case OperationType.Delete:
                logger.LogDebug(
                    "Executing on Adapter Tree: {OperationType} Id={Id}",
                    operation.Type,
                    model.Id);
                break;

            case OperationType.Update:
                logger.LogDebug(
                    "Executing on Adapter Tree: {OperationType} Id={Id} {AltId}, Status=({Status})",
                    operation.Type,
                    model.Id,
                    model.AltId,
                    model.Status);
                break;

            default:
                throw new InvalidEnumArgumentException();
        }
    }
}
