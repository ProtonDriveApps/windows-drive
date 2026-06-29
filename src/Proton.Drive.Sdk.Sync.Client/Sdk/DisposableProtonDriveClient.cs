using Proton.Drive.Sdk;
using Proton.Drive.Shared.Devices;
using Proton.Sdk.Caching;
using Proton.Sdk.Telemetry;

namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class DisposableProtonDriveClient : IDisposable
{
    private readonly SqliteCacheRepository _entityCacheRepository;
    private readonly SqliteCacheRepository _secretCacheRepository;

    public DisposableProtonDriveClient(
        IHttpClientFactory httpClientFactory,
        IAccountClient accountClient,
        IClientInstanceIdentityProvider clientInstanceIdentityProvider,
        Proton.Sdk.IFeatureFlagProvider featureFlagProvider,
        ITelemetry sdkDiagnostics)
    {
        _entityCacheRepository = SqliteCacheRepository.OpenInMemory();
        _secretCacheRepository = SqliteCacheRepository.OpenInMemory();

        try
        {
            Instance = new ProtonDriveClient(
                new SdkHttpClientFactoryDecorator(httpClientFactory),
                accountClient,
                _entityCacheRepository,
                _secretCacheRepository,
                featureFlagProvider,
                sdkDiagnostics,
                new ProtonDriveClientOptions
                {
                    BindingsLanguage = "csharp",
                    Uid = clientInstanceIdentityProvider.GetClientInstanceId(),
                });
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public ProtonDriveClient Instance { get; }

    public void Dispose()
    {
        _entityCacheRepository.Dispose();
        _secretCacheRepository.Dispose();
    }
}
