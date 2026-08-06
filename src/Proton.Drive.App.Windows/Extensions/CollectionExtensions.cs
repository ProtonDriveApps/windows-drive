using Proton.Drive.Shared;

namespace Proton.Drive.App.Windows.Extensions;

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
