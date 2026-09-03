using Proton.Sdk.Caching;

namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class DisposableSdkClient<TClient> : IDisposable
    where TClient : class
{
    private readonly SqliteCacheRepository _cacheRepository;

    public DisposableSdkClient(Func<SqliteCacheRepository, TClient> clientFactory)
    {
        _cacheRepository = SqliteCacheRepository.OpenInMemory();

        try
        {
            Instance = clientFactory(_cacheRepository);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public TClient Instance { get; }

    public void Dispose()
    {
        _cacheRepository.Dispose();
    }
}
