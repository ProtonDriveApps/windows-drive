using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk;
using Proton.Sdk.Telemetry;
using ProtonDrive.Client.Authentication;
using ProtonDrive.Client.Authentication.Sessions;
using ProtonDrive.Client.Authentication.Srp;
using ProtonDrive.Client.BlockVerification;
using ProtonDrive.Client.BugReport;
using ProtonDrive.Client.Contacts;
using ProtonDrive.Client.Core.Events;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Client.Cryptography.TimeProvision;
using ProtonDrive.Client.Devices;
using ProtonDrive.Client.Features;
using ProtonDrive.Client.FileUploading;
using ProtonDrive.Client.Health;
using ProtonDrive.Client.Instrumentation.Observability;
using ProtonDrive.Client.Instrumentation.Observability.Download;
using ProtonDrive.Client.Instrumentation.Observability.Integrity;
using ProtonDrive.Client.Instrumentation.Observability.Upload;
using ProtonDrive.Client.Instrumentation.Telemetry;
using ProtonDrive.Client.MediaTypes;
using ProtonDrive.Client.Notifications;
using ProtonDrive.Client.Notifications.Contracts;
using ProtonDrive.Client.Offline;
using ProtonDrive.Client.RemoteNodes;
using ProtonDrive.Client.Repository;
using ProtonDrive.Client.Sdk;
using ProtonDrive.Client.Sdk.Metrics;
using ProtonDrive.Client.Settings;
using ProtonDrive.Client.Shares;
using ProtonDrive.Client.Shares.Events;
using ProtonDrive.Client.Shares.SharedWithMe;
using ProtonDrive.Client.TlsPinning.Reporting;
using ProtonDrive.Client.Volumes;
using ProtonDrive.Client.Volumes.Events;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Localization;
using ProtonDrive.Shared.Metrics;
using ProtonDrive.Shared.Net.Http.TlsPinning;
using ProtonDrive.Shared.Offline;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.FileSystem;
using Refit;

namespace ProtonDrive.Client.Configuration;

public static class ApiClientConfigurator
{
    public static readonly string SdkHttpClientName = "Sdk";
    public static readonly string AuthHttpClientName = "Auth";
    public static readonly string CoreHttpClientName = "Core";
    public static readonly string DataHttpClientName = "Data";
    public static readonly string DriveHttpClientName = "Drive";
    public static readonly string FeatureHttpClientName = "Feature";
    public static readonly string ContactsHttpClientName = "Contacts";
    public static readonly string FileRevisionUpdateHttpClientName = "FileRevisionUpdate";
    public static readonly string BlocksHttpClientName = "Blocks";
    public static readonly string PaymentsHttpClientName = "Payments";
    public static readonly string TlsPinningReportHttpClientName = "TlsPinningReport";
    public static readonly string ErrorReportHttpClientName = "ErrorReport";

    private const string RefitHttpClientNameSuffix = "-Refit";
    private const string NonCriticalHttpClientNameSuffix = "-NonCritical";

    private static readonly SystemTextJsonContentSerializer DefaultContentSerializer = new(new JsonSerializerOptions { PropertyNamingPolicy = null });
    private static readonly ProtonApiUrlParameterFormatter DefaultUrlParameterFormatter = new();
    private static readonly RefitSettings DefaultRefitSettings = new(DefaultContentSerializer, DefaultUrlParameterFormatter);

    public static IServiceCollection AddFileSystemClient(this IServiceCollection services)
    {
        services.AddSingleton<IFileContentTypeProvider, FileContentTypeProvider>();

        services.AddSingleton<ITelemetry, SdkDiagnostics>();
        services.AddSingleton<SdkFeatureFlagProvider>();
        services.AddSingleton<ISdkClientFactory, SdkClientFactory>();

        services.AddSingleton<SdkMetrics>();
        services.AddSingleton<IMetricsRecorder>(provider => provider.GetRequiredService<SdkMetrics>());

        services.AddSingleton<UploadMetrics>();
        services.AddSingleton<DownloadMetrics>();
        services.AddSingleton<IntegrityMetrics>();

        services.AddSingleton<UploadMetricsCollector>();
        services.AddSingleton<DownloadMetricsCollector>();
        services.AddSingleton<IntegrityMetricsCollector>();

        services.AddSingleton<IMetricsService, MetricsService>();
        services.AddSingleton<UploadMetricsMapper>();
        services.AddSingleton<DownloadMetricsMapper>();
        services.AddSingleton<IntegrityMetricsMapper>();

        services.AddSingleton<IRemoteFileSystemClientFactory, RemoteFileSystemClientFactory>();
        services.AddSingleton<IRemoteEventLogClientFactory, RemoteEventLogClientFactory>();

        services.AddSingleton<IRevisionManifestCreator, RevisionManifestCreator>();
        services.AddSingleton<IExtendedAttributesReader, ExtendedAttributesReader>();
        services.AddSingleton<IRevisionSealerFactory, RevisionSealerFactory>();
        services.AddBlockVerification(DriveHttpClientName + RefitHttpClientNameSuffix, DefaultRefitSettings);

        return services;
    }

    public static IServiceCollection AddApiClients(this IServiceCollection services)
    {
        services.AddSingleton<IScheduler, ThreadPoolScheduler>();
        services.AddSingleton<CookieContainer>();

        services.AddSingleton<IErrorReportingHttpClientConfigurator>(provider => new ErrorReportingHttpClientConfigurator(provider));
        services.AddSingleton<AuthenticationService>();
        services.AddSingleton<ISrpClientFactory, SrpClientFactory>();
        services.AddSingleton<IAuthenticationService>(sp => sp.GetRequiredService<AuthenticationService>());
        services.AddSingleton<ISessionProvider>(sp => sp.GetRequiredService<AuthenticationService>());
        services.AddSingleton(sp => new Lazy<IAuthenticationService>(sp.GetRequiredService<IAuthenticationService>));
        services.AddSingleton(sp => new Lazy<ISessionProvider>(sp.GetRequiredService<ISessionProvider>));

        services.AddSingleton<ISessionClient, SessionClient>();

        services.AddSingleton<OfflineService>();
        services.AddSingleton<IOfflineService>(sp => sp.GetRequiredService<OfflineService>());
        services.AddSingleton<IOfflinePolicyProvider>(sp => sp.GetRequiredService<OfflineService>());

        services.AddSingleton<ProtectedSessionRepository>();
        services.AddSingleton<IProtectedRepository<Session>>(sp => new CachingRepository<Session>(sp.GetRequiredService<ProtectedSessionRepository>()));
        services.AddSingleton(sp => sp.GetRequiredService<IRepositoryFactory>().GetRepository<Session>("Session.json"));
        services.AddSingleton<TlsPinningConfigFactory>();
        services.AddSingleton<TlsPinningHandlerFactory>();

        services.AddTransient<AuthorizationHandler>();
        services.AddTransient<OfflineHandler>();
        services.AddTransient<ChunkedTransferEncodingHandler>();

        services.AddSingleton<CryptographyTimeProvider>();
        services.AddTransient<CryptographyTimeProvisionHandler>();

        services.AddSingleton<IPgpTransformerFactory, PgpTransformerFactory>();
        services.AddSingleton<IKeyPassphraseProvider, KeyPassphraseProvider>();

        services.AddSingleton<ProtectedKeyPassphraseRepository>();
        services.AddSingleton<IProtectedRepository<KeyPassphrases>>(sp => new CachingRepository<KeyPassphrases>(
            sp.GetRequiredService<ProtectedKeyPassphraseRepository>()));
        services.AddSingleton(sp => sp.GetRequiredService<IRepositoryFactory>()
            .GetRepository<KeyPassphrases>("KeyPassphrases.json"));

        services.AddSingleton<TlsPinningReportClient>();
        services.AddSingleton<ITlsPinningReportClient>(provider =>
            new CachingTlsPinningReportClient(
                new SafeTlsPinningReportClient(
                    provider.GetRequiredService<TlsPinningReportClient>())));
        services.AddSingleton<Func<ITlsPinningReportClient>>(provider => provider.GetRequiredService<ITlsPinningReportClient>);

        services.AddSingleton<IUserClient, UserClient>();
        services.AddSingleton<IVolumeCreationParametersFactory, VolumeCreationParametersFactory>();
        services.AddSingleton<IVolumeClient, VolumeClient>();
        services.AddSingleton<IDeviceCreationParametersFactory, DeviceCreationParametersFactory>();
        services.AddSingleton<IDeviceClient, DeviceClient>();
        services.AddSingleton<IContactService, ContactService>();
        services.AddSingleton<ISharedWithMeClient, SharedWithMeClient>();
        services.AddSingleton<IDriveHealthClient, DriveHealthClient>();

        services.AddSingleton<IPhotoHashProvider, PhotoHashProvider>();
        services.AddSingleton<IPhotoDuplicateService, PhotoDuplicateService>();

        services.AddApiHttpClient(SdkHttpClientName, GetDriveBaseAddress, GetDriveApiNumberOfRetries, _ => Timeout.InfiniteTimeSpan)
            .EnableAuthorization()
            ;

        services.AddApiHttpClients(AuthHttpClientName, GetAuthBaseAddress, GetDefaultNumberOfRetries, GetDefaultTimeout)
            .AddApiClient<IAuthenticationApiClient>()
            .AddApiClient<IAuthenticationSessionApiClient>()
            ;

        services.AddApiHttpClients(PaymentsHttpClientName, GetPaymentsBaseAddress, GetDefaultNumberOfRetries, GetDefaultTimeout)
            .AddApiClient<IPaymentsApiClient>()
            ;

        services.AddApiHttpClients(CoreHttpClientName, GetCoreBaseAddress, GetDefaultNumberOfRetries, GetDefaultTimeout)
            .AddApiClient<IUserApiClient>()
            .AddApiClient<IAddressApiClient>()
            .AddApiClient<IKeyApiClient>()
            .AddApiClient<ISettingsApiClient>()
            .AddApiClient<ICoreEventApiClient>()
            ;

        services.AddApiHttpClients(TlsPinningReportHttpClientName, GetCoreBaseAddress, GetDefaultNumberOfRetries, GetDefaultTimeout)
            .AddApiClient<ITlsPinningReportApiClient>();

        services.AddApiHttpClients(DriveHttpClientName, GetDriveBaseAddress, GetDriveApiNumberOfRetries, GetDefaultTimeout)
            .AddApiClient<IVolumeApiClient>()
            .AddApiClient<IVolumeEventApiClient>()
            .AddApiClient<IDeviceApiClient>()
            .AddApiClient<IShareApiClient>()
            .AddApiClient<IShareEventApiClient>()
            .AddApiClient<ILinkApiClient>()
            .AddApiClient<IFolderApiClient>()
            .AddApiClient<IFileApiClient>()
            .AddApiClient<IPhotoApiClient>()
            ;

        services.AddApiHttpClients(
                DriveHttpClientName + NonCriticalHttpClientNameSuffix,
                GetDriveBaseAddress,
                numberOfRetriesSelector: _ => 0,
                GetDefaultTimeout,
                useOfflinePolicy: false)
            .AddApiClient<IDriveUserApiClient>()
            .AddApiClient<IDriveHealthApiClient>()
            ;

        services.AddApiHttpClients(FileRevisionUpdateHttpClientName, GetDriveBaseAddress, GetDriveApiNumberOfRetries, GetRevisionUpdateTimeout)
            .AddApiClient<IFileRevisionUpdateApiClient>()
            ;

        // TODO: inject TLS pinning configuration provider to make it apparent that there is more than the base address to differentiate HTTP clients
        services.AddApiHttpClient(BlocksHttpClientName, GetDriveBaseAddress, GetDriveApiNumberOfRetries, GetBlocksTimeout)
            .EnableAuthorization()
            ;

        services.AddApiHttpClients(
                FeatureHttpClientName + NonCriticalHttpClientNameSuffix,
                GetFeatureBaseAddress,
                numberOfRetriesSelector: _ => 0,
                GetDefaultTimeout,
                useOfflinePolicy: false)
            .AddApiClient<IFeatureApiClient>()
            ;

        services.AddApiHttpClients(
                ContactsHttpClientName + NonCriticalHttpClientNameSuffix,
                GetContactsBaseAddress,
                numberOfRetriesSelector: _ => 0,
                GetDefaultTimeout,
                useOfflinePolicy: false)
            .AddApiClient<IContactApiClient>()
            ;

        services.AddApiHttpClients(
                DataHttpClientName + NonCriticalHttpClientNameSuffix,
                GetDataBaseAddress,
                numberOfRetriesSelector: _ => 0,
                GetDefaultTimeout,
                useOfflinePolicy: false)
            .AddApiClient<ITelemetryApiClient>()
            .AddApiClient<IObservabilityApiClient>()
            ;

        services.AddSingleton<IAddressKeyProvider, AddressKeyProvider>();
        services.AddSingleton<IAccountClient, SdkAccountClient>();
        services.AddSingleton(provider => new Func<IAddressKeyProvider>(provider.GetRequiredService<IAddressKeyProvider>));
        services.AddSingleton<ICryptographyService, CryptographyService>();
        services.AddSingleton<IRemoteNodeService, RemoteNodeService>();
        services.AddSingleton<IRemoteFileMetadataProvider, RemoteFileMetadataProvider>();

        services.AddSingleton<IBugReportClient, BugReportClient>();

        services.AddSingleton<CoreEventClient>();
        services.AddSingleton<ICoreEventClient>(provider => provider.GetRequiredService<CoreEventClient>());
        services.AddSingleton<ICoreEventProvider>(provider => provider.GetRequiredService<CoreEventClient>());

        services.AddSingleton<UserAddressChangeHandler>();

        services.AddSingleton<VolumeEventClient>();
        services.AddSingleton<IVolumeEventClient>(provider => provider.GetRequiredService<VolumeEventClient>());
        services.AddSingleton<ShareEventClient>();
        services.AddSingleton<IShareEventClient>(provider => provider.GetRequiredService<ShareEventClient>());

        services.AddSingleton(
            provider =>
            {
                var appConfig = provider.GetRequiredService<AppConfig>();
                var filePath = Path.Combine(appConfig.AppFolderPath, "Resources\\Notifications", "Notifications.json");

                return provider.GetRequiredService<IRepositoryFactory>()
                    .GetCachingCollectionRepository<Notification>(filePath);
            });

        services.AddSingleton<INotificationClient, NotificationClient>();

        return services;

        static Uri GetAuthBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.AuthBaseUrl, "Missing Auth base URL.");
        static Uri GetCoreBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.CoreBaseUrl, "Missing Core base URL.");
        static Uri GetFeatureBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.FeatureBaseUrl, "Missing Feature base URL.");
        static Uri GetContactsBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.ContactsBaseUrl, "Missing Contacts base URL.");
        static Uri GetDataBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.DataBaseUrl, "Missing Data base URL.");
        static Uri GetDriveBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.DriveBaseUrl, "Missing Drive base URL.");
        static Uri GetPaymentsBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.PaymentsBaseUrl, "Missing Payments base URL.");

        static int GetDefaultNumberOfRetries(DriveApiConfig config) => config.DefaultNumberOfRetries;
        static int GetDriveApiNumberOfRetries(DriveApiConfig config) => config.DriveApiNumberOfRetries;

        static TimeSpan GetDefaultTimeout(DriveApiConfig config) => config.Timeout;
        static TimeSpan GetBlocksTimeout(DriveApiConfig config) => config.BlocksTimeout;
        static TimeSpan GetRevisionUpdateTimeout(DriveApiConfig config) => config.RevisionUpdateTimeout;
    }

    public static void InitializeApiClients(this IServiceProvider provider)
    {
        // UserAddressChangeHandler is not directly referenced, therefore it is instantiated explicitly
        provider.GetRequiredService<UserAddressChangeHandler>();

        PgpEnvironment.DefaultTimeProviderOverride = provider.GetRequiredService<CryptographyTimeProvider>();
    }

    private static ApiClientBuilder AddApiHttpClients(
        this IServiceCollection services,
        string name,
        Func<DriveApiConfig, Uri> baseAddressSelector,
        Func<DriveApiConfig, int> numberOfRetriesSelector,
        Func<DriveApiConfig, TimeSpan> timeoutSelector,
        bool useOfflinePolicy = true)
    {
        services.AddApiHttpClient(name, baseAddressSelector, numberOfRetriesSelector, timeoutSelector, useOfflinePolicy);

        // Separate configuration for Refit that excludes the trailing slash
        var refitHttpClientBuilder = services.AddApiHttpClient(
            name + RefitHttpClientNameSuffix,
            driveApiConfig =>
            {
                var uri = baseAddressSelector.Invoke(driveApiConfig);
                var uriBuilder = new UriBuilder(uri) { Path = uri.AbsolutePath.TrimEnd('/') };
                return uriBuilder.Uri;
            },
            numberOfRetriesSelector,
            timeoutSelector,
            useOfflinePolicy);

        return new ApiClientBuilder(refitHttpClientBuilder);
    }

    private static Uri EnsureEndsWithSlash(Uri? uri, string errorMessage)
    {
        if (uri is null)
        {
            throw new InvalidOperationException(errorMessage);
        }

        var path = uri.AbsolutePath.EndsWith('/') ? uri.AbsolutePath : uri.AbsolutePath + "/";

        var uriBuilder = new UriBuilder(uri) { Path = path };

        return uriBuilder.Uri;
    }

    private static IHttpClientBuilder AddApiHttpClient(
        this IServiceCollection services,
        string name,
        Func<DriveApiConfig, Uri> baseAddressSelector,
        Func<DriveApiConfig, int> numberOfRetriesSelector,
        Func<DriveApiConfig, TimeSpan> timeoutSelector,
        bool useOfflinePolicy = true)
    {
        return services
            .AddHttpClient(
                name,
                (provider, httpClient) =>
                {
                    var config = provider.GetRequiredService<DriveApiConfig>();
                    var languageProvider = provider.GetRequiredService<ILanguageProvider>();
                    var culture = languageProvider.GetCulture();

                    httpClient.BaseAddress = baseAddressSelector.Invoke(config);
                    httpClient.DefaultRequestHeaders.AddApiRequestHeaders(config, culture);
                    httpClient.DefaultRequestHeaders.TransferEncodingChunked = false;

                    // Make sure the HttpClient does not interfere with the TimeoutHandler
                    httpClient.Timeout = Timeout.InfiniteTimeSpan;
                })
            .ApplyHttpClientPrimaryHandler(name)
            .ConfigureHttpClient(numberOfRetriesSelector, timeoutSelector, useOfflinePolicy);
    }

    /// <summary>
    /// Adds authorization header to the request. The <see cref="AuthorizationHandler"/> will take care
    /// of adding required value and other authorization related headers.
    /// </summary>
    /// <remarks>
    /// Make sure the <see cref="AuthorizationHandler"/> is added to the chain of HTTP request handlers.
    /// </remarks>
    private static void EnableAuthorization(this IHttpClientBuilder builder)
    {
        builder.ConfigureHttpClient((_, client) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer"));
    }

    private sealed class ProtonApiUrlParameterFormatter : DefaultUrlParameterFormatter
    {
        public override string? Format(object? parameterValue, ICustomAttributeProvider attributeProvider, Type type)
        {
            if (parameterValue is bool booleanValue)
            {
                return booleanValue ? "1" : "0";
            }

            return base.Format(parameterValue, attributeProvider, type);
        }
    }

    private sealed class ApiClientBuilder
    {
        private readonly IHttpClientBuilder _builder;

        public ApiClientBuilder(IHttpClientBuilder builder)
        {
            _builder = builder;
        }

        public ApiClientBuilder AddApiClient<T>()
            where T : class
        {
            _builder.Services.AddSingleton(_ => RequestBuilder.ForType<T>(DefaultRefitSettings));

            _builder.Services.AddTransient(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(_builder.Name);

                return RestService.For(httpClient, sp.GetRequiredService<IRequestBuilder<T>>());
            });

            return this;
        }
    }
}
