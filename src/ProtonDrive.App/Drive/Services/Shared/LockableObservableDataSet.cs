using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Drive.Services.Shared;

internal class LockableObservableDataSet<TKey, TItem> : IDataSetProvider<TKey, TItem>
    where TKey : IEquatable<TKey>
    where TItem : IIdentifiable<TKey>
{
    private readonly ObservableDataSet<TKey, TItem> _items = new();
    private readonly SemaphoreSlim _semaphore = new(initialCount: 1, maxCount: 1);

    public event EventHandler<ItemsChangedEventArgs<TKey, TItem>>? ItemsChanged
    {
        add => _items.ItemsChanged += value;
        remove => _items.ItemsChanged -= value;
    }

    public event EventHandler<DataServiceState>? StateChanged;

    public DataServiceState State { get; private set; } = DataServiceState.Initial;

    async Task<IReadOnlyDataSet<TKey, TItem>> IDataSetProvider<TKey, TItem>.GetItemsAsync(CancellationToken cancellationToken)
    {
        return await GetItemsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IDataSet<TKey, TItem>> GetItemsAsync(CancellationToken cancellationToken)
    {
        var disposableLock = await _semaphore.LockAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new LockedDataSet<TKey, TItem>(_items, disposableLock);
        }
        catch
        {
            disposableLock.Dispose();
            throw;
        }
    }

    public async Task AddOrUpdateAsync(TItem item, CancellationToken cancellationToken)
    {
        using var items = await GetItemsAsync(cancellationToken).ConfigureAwait(false);

        items.AddOrUpdate(item);
    }

    public async Task RemoveAsync(TKey id, CancellationToken cancellationToken)
    {
        using var items = await GetItemsAsync(cancellationToken).ConfigureAwait(false);

        items.Remove(id);
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        using var items = await GetItemsAsync(cancellationToken).ConfigureAwait(false);

        items.Clear();
    }

    public void OnStateChanged(DataServiceState value)
    {
        State = value;
        StateChanged?.Invoke(this, value);
    }
}
