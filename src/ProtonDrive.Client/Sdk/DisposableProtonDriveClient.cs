using Proton.Drive.Sdk;
using Proton.Sdk.Caching;
using Proton.Sdk.Telemetry;
using ProtonDrive.Shared.Devices;

namespace ProtonDrive.Client.Sdk;

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
                bindingsLanguage: "csharp",
                uid: clientInstanceIdentityProvider.GetClientInstanceId());
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
