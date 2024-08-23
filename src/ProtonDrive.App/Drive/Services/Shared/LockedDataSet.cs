using System;
using System.Collections;
using System.Collections.Generic;
using ProtonDrive.Shared;

namespace ProtonDrive.App.Drive.Services.Shared;

internal sealed class LockedDataSet<TKey, TItem> : IDataSet<TKey, TItem>
    where TKey : IEquatable<TKey>
    where TItem : IIdentifiable<TKey>
{
    private readonly ObservableDataSet<TKey, TItem> _items;
    private readonly IDisposable _disposableLock;

    private bool _disposed;

    public LockedDataSet(ObservableDataSet<TKey, TItem> items, IDisposable disposableLock)
    {
        _items = items;
        _disposableLock = disposableLock;
    }

    public int Count => Items.Count;

    private ObservableDataSet<TKey, TItem> Items
    {
        get
        {
            return _disposed ? throw new ObjectDisposedException(nameof(LockedDataSet<TKey, TItem>)) : _items;
        }
    }

    public IEnumerator<TItem> GetEnumerator() => Items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void AddOrUpdate(TItem item) => Items.AddOrUpdate(item);

    public void Remove(TKey id) => Items.Remove(id);

    public void Clear() => Items.Clear();

    public void Dispose()
    {
        _disposableLock.Dispose();
        _disposed = true;
    }
}
