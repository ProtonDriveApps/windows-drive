using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace ProtonDrive.Shared.Caching;

public static class MemoryCacheExtensions
{
    public static async Task<TItem> GetOrExclusivelyCreateAsync<TItem>(
        this IMemoryCache cache,
        object key,
        Func<Task<TItem>> factory,
        SemaphoreSlim cacheSemaphore,
        CancellationToken cancellationToken)
        where TItem : notnull
    {
        TItem value;
        if (!cache.TryGetValue(key, out var cachedValue) || cachedValue is null)
        {
            var valueSemaphore = await GetValueSemaphoreAsync(cache, key, cacheSemaphore, cancellationToken).ConfigureAwait(false);

            await valueSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (!cache.TryGetValue(key, out cachedValue) || cachedValue is null)
                {
                    value = cache.Set(key, await factory.Invoke().ConfigureAwait(false));
                    cache.Remove(new SemaphoreKey(key));
                }
                else
                {
                    value = (TItem)cachedValue;
                }
            }
            finally
            {
                valueSemaphore.Release();
            }
        }
        else
        {
            value = (TItem)cachedValue;
        }

        return value;
    }

    private static async Task<SemaphoreSlim> GetValueSemaphoreAsync(
        this IMemoryCache cache,
        object key,
        SemaphoreSlim cacheSemaphore,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim valueSemaphore;

        if (!cache.TryGetValue(new SemaphoreKey(key), out var cachedValueSemaphore) || cachedValueSemaphore is null)
        {
            await cacheSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (!cache.TryGetValue(new SemaphoreKey(key), out cachedValueSemaphore) || cachedValueSemaphore is null)
                {
                    valueSemaphore = cache.Set(new SemaphoreKey(key), new SemaphoreSlim(1, 1));
                }
                else
                {
                    valueSemaphore = (SemaphoreSlim)cachedValueSemaphore;
                }
            }
            finally
            {
                cacheSemaphore.Release();
            }
        }
        else
        {
            valueSemaphore = (SemaphoreSlim)cachedValueSemaphore;
        }

        return valueSemaphore;
    }

    private readonly record struct SemaphoreKey(object ValueKey)
    {
        public override int GetHashCode()
        {
            return HashCode.Combine(ValueKey.GetHashCode());
        }
    }
}
