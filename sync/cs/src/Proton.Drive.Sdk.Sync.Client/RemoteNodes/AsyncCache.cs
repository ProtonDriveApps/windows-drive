using Microsoft.Extensions.Caching.Memory;
using Proton.Drive.Shared.Caching;

namespace Proton.Drive.Sdk.Sync.Client.RemoteNodes;

internal sealed class AsyncCache<TKey, TValue>
    where TKey : notnull
    where TValue : notnull
{
    private readonly SemaphoreSlim _cacheSemaphore = new(1, 1);

    private readonly IMemoryCache _cache;

    public AsyncCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<TValue> GetOrAddAsync(TKey key, Func<Task<TValue>> valueFactory, CancellationToken cancellationToken)
    {
        return await _cache.GetOrExclusivelyCreateAsync(key, valueFactory, _cacheSemaphore, cancellationToken).ConfigureAwait(false);
    }
}
