using System;
using System.Collections;
using System.Collections.Generic;
using ProtonDrive.Shared;

namespace ProtonDrive.App.Drive.Services.Shared;

internal class ObservableDataSet<TKey, TItem> : IReadOnlyCollection<TItem>
    where TKey : IEquatable<TKey>
    where TItem : IIdentifiable<TKey>
{
    private readonly Dictionary<TKey, TItem> _items = [];

    public event EventHandler<ItemsChangedEventArgs<TKey, TItem>>? ItemsChanged;

    public int Count => _items.Count;

    public IEnumerator<TItem> GetEnumerator() => _items.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void AddOrUpdate(TItem item)
    {
        if (_items.TryGetValue(item.Id, out var prevItem))
        {
            if (prevItem.Equals(item))
            {
                OnItemsChanged(ItemsChangeType.AttemptedToUpdate, item.Id, item);

                return;
            }

            _items[item.Id] = item;
            OnItemsChanged(ItemsChangeType.Updated, item.Id, item);

            return;
        }

        _items[item.Id] = item;
        OnItemsChanged(ItemsChangeType.Added, item.Id, item);
    }

    public void Remove(TKey id)
    {
        if (_items.Remove(id, out var removedItem))
        {
            OnItemsChanged(ItemsChangeType.Removed, id, removedItem);
        }
        else
        {
            OnItemsChanged(ItemsChangeType.AttemptedToRemove, id, default);
        }
    }

    public void Clear()
    {
        _items.Clear();
        OnItemsChanged(ItemsChangeType.Cleared, default, default);
    }

    private void OnItemsChanged(ItemsChangeType changeType, TKey? key, TItem? item)
    {
        ItemsChanged?.Invoke(this, new ItemsChangedEventArgs<TKey, TItem>(changeType, key, item));
    }
}
