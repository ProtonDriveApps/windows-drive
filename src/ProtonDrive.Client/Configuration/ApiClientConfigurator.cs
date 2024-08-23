using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proton.Security;
using ProtonDrive.BlockVerification;
using ProtonDrive.Client.Authentication;
using ProtonDrive.Client.BugReport;
using ProtonDrive.Client.Core.Events;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Client.Devices;
using ProtonDrive.Client.Features;
using ProtonDrive.Client.FileUploading;
using ProtonDrive.Client.Offline;
using ProtonDrive.Client.RemoteNodes;
using ProtonDrive.Client.Repository;
using ProtonDrive.Client.Sanitization;
using ProtonDrive.Client.Settings;
using ProtonDrive.Client.Shares;
using ProtonDrive.Client.Shares.Events;
using ProtonDrive.Client.Shares.SharedWithMe;
using ProtonDrive.Client.Telemetry;
using ProtonDrive.Client.TlsPinning.Reporting;
using ProtonDrive.Client.Volumes;
using ProtonDrive.Client.Volumes.Events;
using ProtonDrive.Shared.Devices;
using ProtonDrive.Shared.Net.Http.TlsPinning;
using ProtonDrive.Shared.Offline;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.FileSystem;
using Refit;

namespace ProtonDrive.Client.Configuration;

public static class ApiClientConfigurator
{
    public static readonly string AuthHttpClientName = "Auth";
    public static readonly string CoreHttpClientName = "Core";
    public static readonly string DataHttpClientName = "Data";
    public static readonly string DriveHttpClientName = "Drive";
    public static readonly string FeatureHttpClientName = "Feature";
    public static readonly string DocsHttpClientName = "Docs";
    public static readonly string FileRevisionUpdateHttpClientName = "FileRevisionUpdate";
    public static readonly string BlocksHttpClientName = "Blocks";
    public static readonly string PaymentsHttpClientName = "Payments";
    public static readonly string TlsPinningReportHttpClientName = "TlsPinningReport";

    private const string RefitHttpClientNameSuffix = "-Refit";
    private const string NonCriticalHttpClientNameSuffix = "-NonCritical";

    private static readonly SystemTextJsonContentSerializer DefaultContentSerializer = new(new JsonSerializerOptions { PropertyNamingPolicy = null });
    private static readonly ProtonApiUrlParameterFormatter DefaultUrlParameterFormatter = new();
    private static readonly RefitSettings DefaultRefitSettings = new(DefaultContentSerializer, DefaultUrlParameterFormatter);

    public static IServiceCollection AddFileSystemClient(this IServiceCollection services, Action<Exception> reportException)
    {
        services.AddSingleton<Func<FileSystemClientParameters, IFileSystemClient<string>>>(
            sp => fileSystemClientParameters => new RemoteFileSystemClient(
                sp.GetRequiredService<DriveApiConfig>(),
                fileSystemClientParameters,
                sp.GetRequiredService<IClientInstanceIdentityProvider>(),
                sp.GetRequiredService<IRemoteNodeService>(),
                sp.GetRequiredService<ILinkApiClient>(),
                sp.GetRequiredService<IFolderApiClient>(),
                sp.GetRequiredService<IFileApiClient>(),
                sp.GetRequiredService<IVolumeApiClient>(),
                sp.GetRequiredService<ICryptographyService>(),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IRevisionSealerFactory>(),
                sp.GetRequiredService<IRevisionManifestCreator>(),
                sp.GetRequiredService<IBlockVerifierFactory>(),
                sp.GetRequiredService<ILoggerFactory>(),
                reportException));

        services.AddSingleton<IRemoteEventLogClientFactory, RemoteEventLogClientFactory>();

        services.AddSingleton<IRevisionManifestCreator, RevisionManifestCreator>();
        services.AddSingleton<IExtendedAttributesReader, ExtendedAttributesReader>();
        services.AddSingleton<IRevisionSealerFactory, RevisionSealerFactory>();
        services.AddBlockVerification(DriveHttpClientName + RefitHttpClientNameSuffix, DefaultRefitSettings);

        return services;
    }

    public static IServiceCollection AddApiClients(
        this IServiceCollection services,
        string locale)
    {
        services.AddSingleton<IScheduler, ThreadPoolScheduler>();
        services.AddSingleton<CookieContainer>();

        services.AddSingleton<SessionService>();
        services.AddSingleton<ISessionService>(sp => sp.GetRequiredService<SessionService>());
        services.AddSingleton<ISessionProvider>(sp => sp.GetRequiredService<SessionService>());
        services.AddSingleton(sp => new Lazy<ISessionService>(sp.GetRequiredService<ISessionService>));
        services.AddSingleton(sp => new Lazy<ISessionProvider>(sp.GetRequiredService<ISessionProvider>));

        services.AddSingleton<ServerTimeCache>();
        services.AddSingleton<IServerTimeProvider>(sp => sp.GetRequiredService<ServerTimeCache>());

        services.AddSingleton<OfflineService>();
        services.AddSingleton<IOfflineService>(sp => sp.GetRequiredService<OfflineService>());
        services.AddSingleton<IOfflinePolicyProvider>(sp => sp.GetRequiredService<OfflineService>());

        services.AddSingleton<ProtectedSessionRepository>();
        services.AddSingleton<IProtectedRepository<Session>>(sp => new CachingRepository<Session>(
            sp.GetRequiredService<ProtectedSessionRepository>()));
        services.AddSingleton(sp => sp.GetRequiredService<IRepositoryFactory>()
            .GetRepository<Session>("Session.json"));

        services.AddTransient<AuthorizationHandler>();
        services.AddTransient<OfflineHandler>();
        services.AddTransient<ChunkedTransferEncodingHandler>();
        services.AddTransient<ServerTimeRecordingHandler>();

        services.AddSingleton<ISrpClient, SrpClient>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ISrpVerifierGenerator, SrpVerifierGenerator>();
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
        services.AddSingleton<ISharedWithMeClient, SharedWithMeClient>();

        services.AddApiHttpClients(AuthHttpClientName, locale, GetAuthBaseAddress, GetDefaultNumberOfRetries, GetDefaultTimeout)
            .AddApiClient<IAuthenticationApiClient>()
            ;

        services.AddApiHttpClients(PaymentsHttpClientName, locale, GetPaymentsBaseAddress, GetDefaultNumberOfRetries, GetDefaultTimeout)
            .AddApiClient<IPaymentsApiClient>()
            ;

        services.AddApiHttpClients(CoreHttpClientName, locale, GetCoreBaseAddress, GetDefaultNumberOfRetries, GetDefaultTimeout)
            .AddApiClient<IUserApiClient>()
            .AddApiClient<IAddressApiClient>()
            .AddApiClient<IKeyApiClient>()
            .AddApiClient<ISettingsApiClient>()
            .AddApiClient<ICoreEventApiClient>()
            ;

        services.AddApiHttpClients(TlsPinningReportHttpClientName, locale, GetCoreBaseAddress, GetDefaultNumberOfRetries, GetDefaultTimeout)
            .AddApiClient<ITlsPinningReportApiClient>();

        services.AddApiHttpClients(DriveHttpClientName, locale, GetDriveBaseAddress, GetDriveApiNumberOfRetries, GetDefaultTimeout)
            .AddApiClient<IVolumeApiClient>()
            .AddApiClient<IVolumeEventApiClient>()
            .AddApiClient<IDeviceApiClient>()
            .AddApiClient<IShareApiClient>()
            .AddApiClient<IShareEventApiClient>()
            .AddApiClient<ILinkApiClient>()
            .AddApiClient<IFolderApiClient>()
            .AddApiClient<IFileApiClient>()
            ;

        services.AddApiHttpClients(DocsHttpClientName, locale, GetDocsBaseAddress, GetDefaultNumberOfRetries, GetDefaultTimeout, useOfflinePolicy: false)
            .AddApiClient<IDocumentSanitizationApiClient>()
            ;

        services.AddApiHttpClients(
                DriveHttpClientName + NonCriticalHttpClientNameSuffix,
                locale,
                GetDriveBaseAddress,
                numberOfRetriesSelector: _ => 0,
                GetDefaultTimeout,
                useOfflinePolicy: false)
            .AddApiClient<IDriveUserApiClient>()
            ;

        services.AddApiHttpClients(FileRevisionUpdateHttpClientName, locale, GetDriveBaseAddress, GetDriveApiNumberOfRetries, GetRevisionUpdateTimeout)
            .AddApiClient<IFileRevisionUpdateApiClient>()
            ;

        // TODO: inject TLS pinning configuration provider to make it apparent that there is more than the base address to differentiate HTTP clients
        services.AddApiHttpClient(BlocksHttpClientName, locale, GetDriveBaseAddress, GetDriveApiNumberOfRetries, GetBlocksTimeout)
            .EnableAuthorization()
            ;

        services.AddApiHttpClients(
                FeatureHttpClientName + NonCriticalHttpClientNameSuffix,
                locale,
                GetFeatureBaseAddress,
                numberOfRetriesSelector: _ => 0,
                GetDefaultTimeout,
                useOfflinePolicy: false)
            .AddApiClient<IFeatureApiClient>()
            ;

        services.AddApiHttpClients(
                DataHttpClientName + NonCriticalHttpClientNameSuffix,
                locale,
                GetDataBaseAddress,
                numberOfRetriesSelector: _ => 0,
                GetDefaultTimeout,
                useOfflinePolicy: false)
            .AddApiClient<ITelemetryApiClient>()
            ;

        services.AddSingleton<IAddressKeyProvider, AddressKeyProvider>();
        services.AddSingleton(provider => new Func<IAddressKeyProvider>(provider.GetRequiredService<IAddressKeyProvider>));
        services.AddSingleton<ICryptographyService, CryptographyService>();
        services.AddSingleton<IRemoteNodeService, RemoteNodeService>();

        services.AddSingleton<IBugReportClient, BugReportClient>();

        services.AddSingleton<CoreEventClient>();
        services.AddSingleton<ICoreEventClient>(provider => provider.GetRequiredService<CoreEventClient>());
        services.AddSingleton<ICoreEventProvider>(provider => provider.GetRequiredService<CoreEventClient>());

        services.AddSingleton<UserAddressChangeHandler>();

        services.AddSingleton<VolumeEventClient>();
        services.AddSingleton<IVolumeEventClient>(provider => provider.GetRequiredService<VolumeEventClient>());
        services.AddSingleton<ShareEventClient>();
        services.AddSingleton<IShareEventClient>(provider => provider.GetRequiredService<ShareEventClient>());

        return services;

        static Uri GetAuthBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.AuthBaseUrl, "Missing Auth base URL.");
        static Uri GetCoreBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.CoreBaseUrl, "Missing Core base URL.");
        static Uri GetFeatureBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.FeatureBaseUrl, "Missing Feature base URL.");
        static Uri GetDataBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.DataBaseUrl, "Missing Data base URL.");
        static Uri GetDriveBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.DriveBaseUrl, "Missing Drive base URL.");
        static Uri GetPaymentsBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.PaymentsBaseUrl, "Missing Payments base URL.");
        static Uri GetDocsBaseAddress(DriveApiConfig config) => EnsureEndsWithSlash(config.DocsBaseUrl, "Missing Docs base URL.");

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
    }

    private static ApiClientBuilder AddApiHttpClients(
        this IServiceCollection services,
        string name,
        string locale,
        Func<DriveApiConfig, Uri> baseAddressSelector,
        Func<DriveApiConfig, int> numberOfRetriesSelector,
        Func<DriveApiConfig, TimeSpan> timeoutSelector,
        bool useOfflinePolicy = true)
    {
        services.AddApiHttpClient(name, locale, baseAddressSelector, numberOfRetriesSelector, timeoutSelector, useOfflinePolicy);

        // Separate configuration for Refit that excludes the trailing slash
        var refitHttpClientBuilder = services.AddApiHttpClient(
            name + RefitHttpClientNameSuffix,
            locale,
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

        var path = uri.AbsolutePath.EndsWith("/") ? uri.AbsolutePath : uri.AbsolutePath + "/";

        var uriBuilder = new UriBuilder(uri) { Path = path };

        return uriBuilder.Uri;
    }

    private static IHttpClientBuilder AddApiHttpClient(
        this IServiceCollection services,
        string name,
        string locale,
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

                    httpClient.BaseAddress = baseAddressSelector.Invoke(config);
                    httpClient.DefaultRequestHeaders.AddApiRequestHeaders(config, locale);
                    httpClient.DefaultRequestHeaders.TransferEncodingChunked = false;

                    // Make sure the HttpClient does not interfere with the TimeoutHandler
                    httpClient.Timeout = Timeout.InfiniteTimeSpan;
                })
            .ConfigureHttpClient(name, numberOfRetriesSelector, timeoutSelector, useOfflinePolicy);
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
