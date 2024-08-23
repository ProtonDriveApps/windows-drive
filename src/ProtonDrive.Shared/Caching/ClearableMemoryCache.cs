using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Shared.Caching;

public class ClearableMemoryCache : IClearableMemoryCache
{
    private readonly IMemoryCache _origin;

    private readonly CancellationHandle _cancellationHandle = new();
    private IChangeToken _expirationToken;

    public ClearableMemoryCache(IMemoryCache origin)
    {
        _origin = origin;

        _expirationToken = new CancellationChangeToken(_cancellationHandle.Token);
    }

    public ICacheEntry CreateEntry(object key)
    {
        return _origin.CreateEntry(key).AddExpirationToken(_expirationToken);
    }

    public void Dispose()
    {
        _origin.Dispose();
    }

    public void Remove(object key)
    {
        _origin.Remove(key);
    }

    public bool TryGetValue(object key, out object? value)
    {
        return _origin.TryGetValue(key, out value);
    }

    public void Clear()
    {
        _cancellationHandle.Cancel();
        _expirationToken = new CancellationChangeToken(_cancellationHandle.Token);
    }
}
