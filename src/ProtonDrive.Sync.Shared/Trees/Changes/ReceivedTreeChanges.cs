using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ProtonDrive.Shared.Repository;

namespace ProtonDrive.Sync.Shared.Trees.Changes;

public sealed class ReceivedTreeChanges<TId> : IReceivedTreeChanges<TId>
    where TId : IEquatable<TId>, IComparable<TId>
{
    private readonly IRepository<TId> _lastConsumedProperty;
    private readonly ConcurrentQueue<TreeChange<TId>> _treeChanges = new();

    private ITreeChangeProvider<TId>? _treeChangeProvider;
    private bool _hasSkippedConsumedItems;
    private volatile TreeChange<TId>? _lastConsumedItem;

    public ReceivedTreeChanges(
        IRepository<TId> lastConsumedProperty,
        ITransactionProvider transactionProvider)
    {
        _lastConsumedProperty = lastConsumedProperty;

        transactionProvider.TransactionCommitted += OnTransactionProviderTransactionCommitted;
    }

    public event EventHandler? Added;

    public bool IsEmpty => _treeChanges.IsEmpty;

    public ReceivedTreeChanges<TId> ConnectTo(ITreeChangeProvider<TId> treeChangeProvider)
    {
        if (_treeChangeProvider is not null)
        {
            throw new InvalidOperationException("Already connected to the provider");
        }

        _treeChangeProvider = treeChangeProvider;

        treeChangeProvider.TreeChanged += OnTreeChangeProviderTreeChanged;

        return this;
    }

    public IEnumerator<TreeChange<TId>> GetEnumerator()
    {
        TreeChange<TId>? previousItem = null;

        while (_treeChanges.TryPeek(out var item))
        {
            if (item == previousItem)
            {
                throw new InvalidOperationException("Consumption of the previous item is not acknowledged");
            }

            previousItem = item;

            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void AcknowledgeConsumed(TreeChange<TId> treeChange)
    {
        if (!_treeChanges.TryDequeue(out var item) || treeChange != item)
        {
            throw new InvalidOperationException("Not a current item provided by the enumerator");
        }

        _lastConsumedItem = treeChange;
        SetLastConsumedId(treeChange.Id);
    }

    private void OnTreeChangeProviderTreeChanged(object? sender, TreeChange<TId> treeChange)
    {
        // We expect the stream of tree changes to have increasing identity values,
        // no item is sent more than once during the object instance lifetime.
        // We skip incoming items until the first not yet consumed item.
        if (!_hasSkippedConsumedItems)
        {
            var lastConsumedId = GetLastConsumedId();

            if (treeChange.Id.CompareTo(lastConsumedId) <= 0)
            {
                // This change has been consumed
                _lastConsumedItem = treeChange;

                return;
            }

            _hasSkippedConsumedItems = true;
        }

        _treeChanges.Enqueue(treeChange);
        OnAdded();
    }

    private void OnTransactionProviderTransactionCommitted(object? sender, EventArgs e)
    {
        var lastConsumedItem = _lastConsumedItem;

        if (lastConsumedItem == null)
        {
            return;
        }

        if (_treeChangeProvider is null)
        {
            throw new InvalidOperationException("Not connected to provider");
        }

        _treeChangeProvider.AcknowledgeConsumed(lastConsumedItem);

        Interlocked.CompareExchange(ref _lastConsumedItem, null, lastConsumedItem);
    }

    private void OnAdded()
    {
        Added?.Invoke(this, EventArgs.Empty);
    }

    private TId? GetLastConsumedId()
    {
        return _lastConsumedProperty.Get();
    }

    private void SetLastConsumedId(TId value)
    {
        _lastConsumedProperty.Set(value);
    }
}
