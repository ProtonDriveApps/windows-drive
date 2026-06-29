using Microsoft.Extensions.Caching.Memory;

namespace Proton.Drive.Shared.Caching;

public interface IClearableMemoryCache : IMemoryCache
{
    void Clear();
}
