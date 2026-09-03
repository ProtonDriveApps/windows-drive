using Proton.Drive.Shared.Devices;
using Proton.Sdk.Caching;
using Proton.Sdk.Configuration;
using Proton.Sdk.Telemetry;

namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class DisposableProtonDriveClient : IDisposable
{
    private readonly SqliteCacheRepository _cacheRepository;

    public DisposableProtonDriveClient(
        IHttpClientFactory httpClientFactory,
        IProtonAccountClient accountClient,
        IClientInstanceIdentityProvider clientInstanceIdentityProvider,
        IFeatureFlagProvider featureFlagProvider,
        ITelemetry sdkDiagnostics)
    {
        _cacheRepository = SqliteCacheRepository.OpenInMemory();

        try
        {
            Instance = new ProtonDriveClient(
                new SdkHttpClientFactoryDecorator(httpClientFactory),
                accountClient,
                _cacheRepository,
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
        _cacheRepository.Dispose();
    }
}
