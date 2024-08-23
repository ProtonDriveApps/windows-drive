using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProtonDrive.Shared;

namespace ProtonDrive.App.Windows.Extensions;

public class ObservableCollectionOfIdentifiableItems<T, TId> : ObservableCollection<T>
    where T : IIdentifiable<TId>
    where TId : IEquatable<TId>
{
    protected HashSet<TId> Ids { get; } = [];

    public bool AddOrReplace(T item, out T? replacedItem)
    {
        if (Ids.Contains(item.Id))
        {
            return Replace(item, out replacedItem);
        }

        Add(item);
        replacedItem = default;

        return false;
    }

    public bool Replace(T item, out T? replacedItem)
    {
        return this.ReplaceFirst(item, out replacedItem, i => i.Id.Equals(item.Id));
    }

    public bool Remove(TId id)
    {
        if (Ids.Contains(id))
        {
            return this.RemoveFirst(i => i.Id.Equals(id));
        }

        return false;
    }

    protected override void ClearItems()
    {
        base.ClearItems();
        Ids.Clear();
    }

    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item);
        Ids.Add(item.Id);
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];
        base.RemoveItem(index);
        Ids.Remove(item.Id);
    }

    protected override void SetItem(int index, T item)
    {
        var replacedItem = this[index];
        base.SetItem(index, item);

        if (!replacedItem.Id.Equals(item.Id))
        {
            Ids.Remove(replacedItem.Id);
            Ids.Add(item.Id);
        }
    }
}
