using System;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Shared.Trees.Operations;

public class OperationLogging<TId>
    where TId : IEquatable<TId>
{
    private readonly string _prefix;
    private readonly ILogger _logger;

    public OperationLogging(string prefix, ILogger logger)
    {
        _prefix = prefix;
        _logger = logger;
    }

    public void LogOperation(Operation<FileSystemNodeModel<TId>> operation)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var model = operation.Model;

        switch (operation.Type)
        {
            case OperationType.Create:
                _logger.LogDebug(
                    "{Prefix}: {OperationType} {Type} \"{Name}\" Id={ParentId}/{Id}, ContentVersion={ContentVersion}",
                    _prefix,
                    operation.Type,
                    model.Type,
                    model.Name,
                    model.ParentId,
                    model.Id,
                    model.ContentVersion);
                break;

            case OperationType.Edit:
                _logger.LogDebug(
                    "{Prefix}: {OperationType} Id={Id}, ContentVersion={ContentVersion}",
                    _prefix,
                    operation.Type,
                    model.Id,
                    model.ContentVersion);
                break;

            case OperationType.Move:
                _logger.LogDebug(
                    "{Prefix}: {OperationType} \"{Name}\" Id={ParentId}/{Id}",
                    _prefix,
                    operation.Type,
                    model.Name,
                    model.ParentId,
                    model.Id);
                break;

            case OperationType.Delete:
                _logger.LogDebug(
                    "{Prefix}: {OperationType} Id={Id}",
                    _prefix,
                    operation.Type,
                    model.Id);
                break;

            default:
                throw new InvalidOperationException();
        }
    }
}
