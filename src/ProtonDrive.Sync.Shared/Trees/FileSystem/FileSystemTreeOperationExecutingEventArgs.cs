using System;
using ProtonDrive.Shared;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public sealed class FileSystemTreeOperationExecutingEventArgs<TModel, TId> : EventArgs
    where TModel : FileSystemNodeModel<TId>, new()
    where TId : IEquatable<TId>
{
    private TModel? _newModel;

    public FileSystemTreeOperationExecutingEventArgs(OperationType type, TModel? oldModel, TModel? newModel)
    {
        Ensure.IsFalse(type == OperationType.Create && oldModel != null, $"Value of {nameof(oldModel)} must be NULL", nameof(oldModel));
        Ensure.IsFalse(type == OperationType.Delete && newModel != null, $"Value of {nameof(newModel)} must be NULL", nameof(newModel));
        Ensure.IsFalse(
            type is OperationType.Move or OperationType.Edit or OperationType.Update && (oldModel == null || newModel == null),
            $"Values of {nameof(oldModel)} and {nameof(newModel)} cannot be NULL");

        Type = type;
        OldModel = oldModel;
        NewModel = newModel;
    }

    public OperationType Type { get; }
    public TModel? OldModel { get; }

    public TModel? NewModel
    {
        get => _newModel;
        set
        {
            if (value is null && Type is OperationType.Create)
            {
                throw new InvalidOperationException($"The {nameof(NewModel)} cannot be NULL for {nameof(OperationType.Create)} operation");
            }

            if (value is not null && Type is OperationType.Delete)
            {
                throw new InvalidOperationException($"The {nameof(NewModel)} must be NULL for {nameof(OperationType.Delete)} operation");
            }

            _newModel = value;
        }
    }
}
