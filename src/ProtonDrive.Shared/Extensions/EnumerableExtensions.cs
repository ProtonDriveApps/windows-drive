using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProtonDrive.Shared.Extensions;

public static class EnumerableExtensions
{
    public static IReadOnlyCollection<TResult> Select<T, TResult>(this IReadOnlyCollection<T> source, Func<T, TResult> selector)
    {
        return Enumerable.Select(source, selector).AsReadOnlyCollection(source.Count);
    }

    public static IReadOnlyCollection<T> Prepend<T>(this IReadOnlyCollection<T> source, T element)
    {
        return Enumerable.Prepend(source, element).AsReadOnlyCollection(source.Count + 1);
    }

    public static IReadOnlyCollection<T> Append<T>(this IReadOnlyCollection<T> source, T element)
    {
        return Enumerable.Append(source, element).AsReadOnlyCollection(source.Count + 1);
    }

    public static List<T> ToList<T>(this IReadOnlyCollection<T> collection)
    {
        var result = new List<T>(collection.Count);
        result.AddRange(collection);
        return result;
    }

    public static IReadOnlyCollection<T> AsReadOnlyCollection<T>(this IEnumerable<T> enumerable, int count)
    {
        return new EnumerableToCollectionWrapper<T>(enumerable, count);
    }

    private sealed class EnumerableToCollectionWrapper<T> : IReadOnlyCollection<T>
    {
        private readonly IEnumerable<T> _enumerable;

        public EnumerableToCollectionWrapper(IEnumerable<T> enumerable, int count)
        {
            _enumerable = enumerable;
            Count = count;
        }

        public int Count { get; }

        public IEnumerator<T> GetEnumerator() => _enumerable.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _enumerable.GetEnumerator();
    }
}
