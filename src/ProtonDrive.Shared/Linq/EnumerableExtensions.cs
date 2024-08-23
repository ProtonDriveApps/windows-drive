using System.Collections.Generic;

namespace ProtonDrive.Shared.Linq;

public static class EnumerableExtensions
{
    /// <summary>
    /// Wraps an item into a <see cref="IEnumerable{T}"/> consisting of a single item.
    /// </summary>
    /// <typeparam name="T">Type of the item.</typeparam>
    /// <param name="item">The item that will be wrapped.</param>
    /// <returns>A <see cref="IEnumerable{T}"/> consisting of a single item.</returns>
    public static IEnumerable<T> Yield<T>(this T item)
    {
        yield return item;
    }
}
