using Proton.Drive.Sdk;
using Proton.Drive.Sdk.Sync.Client.Authentication;
using Proton.Drive.Shared.Devices;
using Proton.Sdk.Telemetry;

namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class SdkClientFactory : ISdkClientFactory, ISdkPhotosClientFactory, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAccountClient _accountClient;
    private readonly IClientInstanceIdentityProvider _clientInstanceIdentityProvider;
    private readonly SdkFeatureFlagProvider _sdkFeatureFlagProvider;
    private readonly ITelemetry _sdkDiagnostics;

    private DisposableSdkClient<ProtonDriveClient>? _sdkClient;
    private DisposableSdkClient<ProtonPhotosClient>? _sdkPhotosClient;
    private bool _sessionStarted;

    public SdkClientFactory(
        IAuthenticationService authenticationService,
        IHttpClientFactory httpClientFactory,
        IAccountClient accountClient,
        IClientInstanceIdentityProvider clientInstanceIdentityProvider,
        SdkFeatureFlagProvider sdkSdkFeatureFlagProvider,
        ITelemetry sdkDiagnostics)
    {
        _httpClientFactory = new SdkHttpClientFactoryDecorator(httpClientFactory);
        _accountClient = accountClient;
        _clientInstanceIdentityProvider = clientInstanceIdentityProvider;
        _sdkFeatureFlagProvider = sdkSdkFeatureFlagProvider;
        _sdkDiagnostics = sdkDiagnostics;

        authenticationService.SessionStarted += (_, _) => _sessionStarted = true;
    }

    public ProtonDriveClient GetOrCreateClient()
    {
        if (_sessionStarted)
        {
            InvalidateSessionClients();
        }

        return (_sdkClient ??= CreateClient()).Instance;
    }

    public ProtonPhotosClient GetOrCreatePhotosClient()
    {
        if (_sessionStarted)
        {
            InvalidateSessionClients();
        }

        return (_sdkPhotosClient ??= CreatePhotosClient()).Instance;
    }

    public void Dispose()
    {
        _sdkClient?.Dispose();
        _sdkPhotosClient?.Dispose();
    }

    private DisposableSdkClient<ProtonDriveClient> CreateClient()
    {
        return new DisposableSdkClient<ProtonDriveClient>((entityRepo, secretRepo) =>
            new ProtonDriveClient(
                new SdkHttpClientFactoryDecorator(_httpClientFactory),
                _accountClient,
                entityRepo,
                secretRepo,
                _sdkFeatureFlagProvider,
                _sdkDiagnostics,
                GetClientOptions()));
    }

    private DisposableSdkClient<ProtonPhotosClient> CreatePhotosClient()
    {
        return new DisposableSdkClient<ProtonPhotosClient>((entityRepo, secretRepo) =>
            new ProtonPhotosClient(
                new SdkHttpClientFactoryDecorator(_httpClientFactory),
                _accountClient,
                entityRepo,
                secretRepo,
                _sdkFeatureFlagProvider,
                _sdkDiagnostics,
                GetClientOptions()));
    }

    private ProtonDriveClientOptions GetClientOptions()
    {
        return new ProtonDriveClientOptions
        {
            BindingsLanguage = "csharp",
            Uid = _clientInstanceIdentityProvider.GetClientInstanceId(),
        };
    }

    private void InvalidateSessionClients()
    {
        _sdkClient?.Dispose();
        _sdkPhotosClient?.Dispose();
        _sdkClient = null;
        _sdkPhotosClient = null;
        _sessionStarted = false;
    }
}
