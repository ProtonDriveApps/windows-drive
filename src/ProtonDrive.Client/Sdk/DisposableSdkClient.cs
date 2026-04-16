using Proton.Sdk.Caching;

namespace ProtonDrive.Client.Sdk;

internal sealed class DisposableSdkClient<TClient> : IDisposable
    where TClient : class
{
    private readonly SqliteCacheRepository _entityCacheRepository;
    private readonly SqliteCacheRepository _secretCacheRepository;

    public DisposableSdkClient(Func<SqliteCacheRepository, SqliteCacheRepository, TClient> clientFactory)
    {
        _entityCacheRepository = SqliteCacheRepository.OpenInMemory();
        _secretCacheRepository = SqliteCacheRepository.OpenInMemory();

        try
        {
            Instance = clientFactory(_entityCacheRepository, _secretCacheRepository);
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
        _entityCacheRepository.Dispose();
        _secretCacheRepository.Dispose();
    }
}
