using System;
using System.Collections.ObjectModel;

namespace ProtonDrive.App.Windows.Extensions;

public static class ObservableCollectionExtensions
{
    public static bool ReplaceFirst<T>(this ObservableCollection<T> collection, T item, out T? replacedItem, Func<T, bool> predicate)
    {
        for (var i = 0; i < collection.Count; i++)
        {
            if (!predicate(collection[i]))
            {
                continue;
            }

            replacedItem = collection[i];
            collection[i] = item;
            return true;
        }

        replacedItem = default;
        return false;
    }

    public static bool RemoveFirst<T>(this ObservableCollection<T> collection, Func<T, bool> predicate)
    {
        for (var i = 0; i < collection.Count; i++)
        {
            if (predicate(collection[i]))
            {
                collection.RemoveAt(i);

                return true;
            }
        }

        return false;
    }
}
