using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Trees.Changes;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;

namespace Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Changes;

internal sealed class RemovingDuplicatesDetectedTreeChangesDecorator<TId> : IDetectedTreeChanges<TId>
    where TId : IEquatable<TId>
{
    private readonly IDetectedTreeChanges<TId> _decoratedInstance;

    private readonly IEqualityComparer<FileSystemNodeModel<TId>> _linkEqualityComparer = new FileSystemNodeModelLinkEqualityComparer<TId>();
    private readonly IEqualityComparer<FileSystemNodeModel<TId>> _attributesEqualityComparer = new FileSystemNodeModelAttributesEqualityComparer<TId>();

    private Operation<FileSystemNodeModel<TId>>? _lastAddedOperation;

    public RemovingDuplicatesDetectedTreeChangesDecorator(
        IDetectedTreeChanges<TId> decoratedInstance,
        ITransactionProvider transactionProvider)
    {
        _decoratedInstance = decoratedInstance;

        transactionProvider.TransactionCommitted += OnTransactionCommitted;
    }

    public void Add(Operation<FileSystemNodeModel<TId>> operation)
    {
        // As an optimization, prevent adding the similar operation
        // with the same node model twice in a row during the same transaction
        if (_lastAddedOperation is not null &&
            IsDelete(operation.Type) == IsDelete(_lastAddedOperation.Type) &&
            Equals(operation.Model, _lastAddedOperation.Model))
        {
            return;
        }

        _lastAddedOperation = operation;

        _decoratedInstance.Add(operation);
    }

    public bool Contains(TId id)
    {
        return _decoratedInstance.Contains(id);
    }

    private static bool IsDelete(OperationType operationType)
    {
        return operationType is OperationType.Delete;
    }

    private void OnTransactionCommitted(object? sender, EventArgs e)
    {
        _lastAddedOperation = null;
    }

    private bool Equals(FileSystemNodeModel<TId> nodeModelA, FileSystemNodeModel<TId> nodeModelB)
    {
        return nodeModelA.Id.Equals(nodeModelB.Id) &&
               _linkEqualityComparer.Equals(nodeModelA, nodeModelB) &&
               _attributesEqualityComparer.Equals(nodeModelA, nodeModelB);
    }
}
