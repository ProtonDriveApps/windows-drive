using System;
using System.Collections.Generic;
using System.Threading;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Shared.Trees.Changes;

public sealed class DetectedTreeChanges<TId> : IDetectedTreeChanges<TId>, ITreeChangeProvider<TId>
    where TId : IEquatable<TId>
{
    private readonly IIdentitySource<TId> _identitySource;
    private readonly ITreeChangeRepository<TId> _repository;
    private readonly ITransactedScheduler _syncScheduler;
    private readonly Queue<TreeChange<TId>> _uncommittedChanges = new();

    private volatile TreeChange<TId>? _lastConsumedItem;

    public DetectedTreeChanges(
        IIdentitySource<TId> identitySource,
        ITreeChangeRepository<TId> repository,
        ITransactionProvider transactionProvider,
        ITransactedScheduler syncScheduler)
    {
        _identitySource = identitySource;
        _repository = repository;
        _syncScheduler = syncScheduler;

        transactionProvider.TransactionStarted += OnTransactionProviderTransactionStarted;
        transactionProvider.TransactionCommitted += OnTransactionProviderTransactionCommitted;
    }

    public event EventHandler<TreeChange<TId>>? TreeChanged;

    public bool IsEmpty => _uncommittedChanges.Count == 0;

    public void Initialize()
    {
        _identitySource.InitializeFrom(default);

        // Tree changes coming from the repository are already committed, but we add them
        // to the queue of uncommitted changes to be send out after the next commit.
        foreach (var item in _repository.GetAll())
        {
            _uncommittedChanges.Enqueue(item);
        }
    }

    public void Add(Operation<FileSystemNodeModel<TId>> operation)
    {
        var treeChange = new TreeChange<TId>(_identitySource.NextValue(), operation);

        _uncommittedChanges.Enqueue(treeChange);
        _repository.Add(treeChange);
    }

    public bool Contains(TId id) => _repository.ContainsNode(id);

    public void AcknowledgeConsumed(TreeChange<TId> lastConsumedItem)
    {
        _lastConsumedItem = lastConsumedItem;

        ScheduleCommit();
    }

    private void OnTransactionProviderTransactionStarted(object? sender, EventArgs e)
    {
        var consumedItem = _lastConsumedItem;

        if (consumedItem == null)
        {
            return;
        }

        _repository.DeleteUpTo(consumedItem.Id);

        // As an optimization, to prevent unnecessary repository access on the next transaction,
        // we set the last consumed item reference to null, if it has not changed yet.
        Interlocked.CompareExchange(ref _lastConsumedItem, null, consumedItem);
    }

    private void OnTransactionProviderTransactionCommitted(object? sender, EventArgs e)
    {
        while (_uncommittedChanges.TryDequeue(out var item))
        {
            OnTreeChanged(item);
        }
    }

    private void OnTreeChanged(TreeChange<TId> treeChange)
    {
        TreeChanged?.Invoke(this, treeChange);
    }

    private void ScheduleCommit()
    {
        _syncScheduler.ScheduleAndCommit(() => { });
    }
}
