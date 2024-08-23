using Microsoft.Extensions.Caching.Memory;

namespace ProtonDrive.Shared.Caching;

public interface IClearableMemoryCache : IMemoryCache
{
    void Clear();
}
