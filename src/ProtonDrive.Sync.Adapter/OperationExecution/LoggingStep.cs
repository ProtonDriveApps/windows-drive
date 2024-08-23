using System;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.OperationExecution;

internal class LoggingStep<TId, TAltId>
    where TId : struct, IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<OperationExecutionPipeline<TId, TAltId>> _logger;

    public LoggingStep(ILogger<OperationExecutionPipeline<TId, TAltId>> logger)
    {
        _logger = logger;
    }

    public void LogSuccess(
        ExecutableOperation<TId> operation,
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo)
    {
        var model = operation.Model;
        var type = nodeInfo.IsDirectory() ? NodeType.Directory : NodeType.File;

        switch (operation.Type)
        {
            case OperationType.Create:
                _logger.LogInformation(
                    "Executed {OperationType} {Type} \"{Root}\"/{Id} {ExternalId} at parent {ParentId} {ParentExternalId}",
                    operation.Type,
                    type,
                    nodeInfo.Root?.Id,
                    model.Id,
                    nodeInfo.GetCompoundId(),
                    model.ParentId,
                    nodeInfo.GetCompoundParentId());
                break;

            case OperationType.Edit:
                _logger.LogInformation(
                    "Executed {OperationType} {Type} \"{Root}\"/{Id} {ExternalId}, ContentVersion={ContentVersion}",
                    operation.Type,
                    type,
                    nodeInfo.Root?.Id,
                    model.Id,
                    nodeInfo.GetCompoundId(),
                    model.ContentVersion);
                break;

            case OperationType.Move:
                var rename = string.IsNullOrEmpty(destinationInfo!.Path);

                if (rename)
                {
                    _logger.LogInformation(
                        "Executed {OperationType} {Type} \"{Root}\"/{Id} {ExternalId}",
                        "Rename",
                        type,
                        nodeInfo.Root?.Id,
                        model.Id,
                        nodeInfo.GetCompoundId());
                }
                else
                {
                    _logger.LogInformation(
                        "Executed {OperationType} {Type} \"{Root}\"/{Id} {ExternalId} to parent \"{DestinationRoot}\"/{DestinationParentId} {DestinationParentExternalId}",
                        operation.Type,
                        type,
                        nodeInfo.Root?.Id,
                        model.Id,
                        nodeInfo.GetCompoundId(),
                        destinationInfo.Root?.Id,
                        model.ParentId,
                        destinationInfo.GetCompoundParentId());
                }

                break;

            case OperationType.Delete:
                _logger.LogInformation(
                    "Executed {OperationType} {Type} \"{Root}\"/{Id} {ExternalId} at parent {ParentId} {ParentExternalId}",
                    operation.Type,
                    type,
                    nodeInfo.Root?.Id,
                    model.Id,
                    nodeInfo.GetCompoundId(),
                    model.ParentId,
                    nodeInfo.GetCompoundParentId());
                break;

            default:
                throw new InvalidOperationException($"The {nameof(OperationType)} has unknown value {operation.Type}");
        }
    }

    public void LogFailure(
        ExecutableOperation<TId> operation,
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo,
        Exception exception)
    {
        var model = operation.Model;
        var type = nodeInfo.IsDirectory() ? NodeType.Directory : NodeType.File;

        switch (operation.Type)
        {
            case OperationType.Create:
                _logger.LogWarning(
                    "Executing {OperationType} {Type} \"{Root}\"/{Id} at parent with Id={ParentId} {ParentExternalId} failed: {ErrorCode} {ErrorMessage}",
                    operation.Type,
                    type,
                    nodeInfo.Root?.Id,
                    model.Id,
                    model.ParentId,
                    nodeInfo.GetCompoundParentId(),
                    exception.GetRelevantFormattedErrorCode(),
                    exception.CombinedMessage());
                break;

            case OperationType.Edit:
                _logger.LogWarning(
                    "Executing {OperationType} {Type} \"{Root}\"/{Id} {ExternalId}, ContentVersion={ContentVersion} failed: {ErrorCode} {ErrorMessage}",
                    operation.Type,
                    type,
                    nodeInfo.Root?.Id,
                    model.Id,
                    nodeInfo.GetCompoundId(),
                    model.ContentVersion,
                    exception.GetRelevantFormattedErrorCode(),
                    exception.CombinedMessage());
                break;

            case OperationType.Move:
                var rename = string.IsNullOrEmpty(destinationInfo!.Path);

                if (rename)
                {
                    _logger.LogWarning(
                        "Executing {OperationType} {Type} \"{Root}\"/{Id} {ExternalId} failed: {ErrorCode} {ErrorMessage}",
                        "Rename",
                        type,
                        nodeInfo.Root?.Id,
                        model.Id,
                        nodeInfo.GetCompoundId(),
                        exception.GetRelevantFormattedErrorCode(),
                        exception.CombinedMessage());
                }
                else
                {
                    _logger.LogWarning(
                        "Executing {OperationType} {Type} \"{Root}\"/{Id} {ExternalId} to parent Id={DestinationParentId} {DestinationParentExternalId} failed: {ErrorCode} {ErrorMessage}",
                        operation.Type,
                        type,
                        nodeInfo.Root?.Id,
                        model.Id,
                        nodeInfo.GetCompoundId(),
                        model.ParentId,
                        destinationInfo.GetCompoundParentId(),
                        exception.GetRelevantFormattedErrorCode(),
                        exception.CombinedMessage());
                }

                break;

            case OperationType.Delete:
                _logger.LogWarning(
                    "Executing {OperationType} {Type} \"{Root}\"/{Id} {ExternalId} failed: {ErrorCode} {ErrorMessage}",
                    operation.Type,
                    type,
                    nodeInfo.Root?.Id,
                    model.Id,
                    nodeInfo.GetCompoundId(),
                    exception.GetRelevantFormattedErrorCode(),
                    exception.CombinedMessage());
                break;

            default:
                throw new InvalidOperationException($"The {nameof(OperationType)} has unknown value {operation.Type}");
        }
    }
}
