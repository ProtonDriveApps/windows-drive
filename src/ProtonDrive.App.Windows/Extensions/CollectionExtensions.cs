using System.Collections.Generic;
using ProtonDrive.Shared;

namespace ProtonDrive.App.Windows.Extensions;

public static class CollectionExtensions
{
    public static void AddEach<T>(this ICollection<T> collection, IEnumerable<T> itemsToAdd)
    {
        Ensure.NotNull(collection, nameof(collection));

        foreach (var item in itemsToAdd)
        {
            collection.Add(item);
        }
    }
}
