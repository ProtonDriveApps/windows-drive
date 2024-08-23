using System;
using System.Collections.Generic;
using ProtonDrive.Shared;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Propagation;

internal class OperationsFactory<TId>
    where TId : IEquatable<TId>
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly Dictionary<UpdateStatus, OperationType> UpdateStatusToOperationType = new()
    {
        { UpdateStatus.Created,         OperationType.Create },
        { UpdateStatus.Edited,          OperationType.Edit },
        { UpdateStatus.Renamed,         OperationType.Move },
        { UpdateStatus.Moved,           OperationType.Move },
        { UpdateStatus.RenamedAndMoved, OperationType.Move },
        { UpdateStatus.Deleted,         OperationType.Delete },
    };

    public ExecutableOperation<TId> Operation(
        AltIdentifiableFileSystemNodeModel<TId, TId> nodeModel,
        IFileSystemNodeModel<TId>? originalNodeModel,
        UpdateStatus status,
        bool backup)
    {
        var operationType = UpdateStatusToOperationType[status];
        var model = new AltIdentifiableFileSystemNodeModel<TId, TId>();

        switch (operationType)
        {
            case OperationType.Create:
                model = model.CopiedFrom(nodeModel);
                break;

            case OperationType.Edit:
                Ensure.NotNull(originalNodeModel, nameof(originalNodeModel));
                model = model.CopiedFrom(originalNodeModel)
                    .WithAttributesFrom(nodeModel)
                    .WithAltId(nodeModel.AltId);
                break;

            case OperationType.Move:
                Ensure.NotNull(originalNodeModel, nameof(originalNodeModel));
                model = model.CopiedFrom(originalNodeModel);
                if (status.Contains(UpdateStatus.Renamed))
                {
                    model = model.WithName<AltIdentifiableFileSystemNodeModel<TId, TId>, TId>(nodeModel.Name);
                }

                if (status.Contains(UpdateStatus.Moved))
                {
                    model = model.WithParentId(nodeModel.ParentId);
                }

                break;

            case OperationType.Delete:
                Ensure.NotNull(originalNodeModel, nameof(originalNodeModel));
                model = model.CopiedFrom(originalNodeModel);
                break;
        }

        // Backup is supported for Edit operations only
        backup = backup && operationType == OperationType.Edit;

        return new ExecutableOperation<TId>(operationType, model, backup);
    }
}
