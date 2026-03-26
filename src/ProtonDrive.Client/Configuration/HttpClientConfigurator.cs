using Microsoft.Extensions.DependencyInjection;
using ProtonDrive.Client.Authentication;
using ProtonDrive.Client.Cryptography.TimeProvision;
using ProtonDrive.Client.Offline;
using ProtonDrive.Shared.HumanVerification;
using ProtonDrive.Shared.Net.Http;

namespace ProtonDrive.Client.Configuration;

public static class HttpClientConfigurator
{
    public static IHttpClientBuilder ApplyHttpClientPrimaryHandler(this IHttpClientBuilder builder, string name)
    {
        return builder
            .UseSocketsHttpHandler((socketsHttpHandler, serviceProvider) => ConfigurePrimaryHttpMessageHandler(socketsHttpHandler, name, serviceProvider));
    }

    public static void ConfigurePrimaryHttpMessageHandler(SocketsHttpHandler socketsHttpHandler, string name, IServiceProvider serviceProvider)
    {
        socketsHttpHandler
            .AddAutomaticDecompression()
            .ConfigureCookies(serviceProvider)
            .AddTlsPinning(name, serviceProvider);
    }

    public static IHttpClientBuilder ConfigureHttpClient(
        this IHttpClientBuilder builder,
        Func<DriveApiConfig, int> numberOfRetriesSelector,
        Func<DriveApiConfig, TimeSpan> timeoutSelector,
        bool useOfflinePolicy = true)
    {
        builder
            .AddHttpMessageHandler<HumanVerificationHandler>()
            .AddHttpMessageHandler<ChunkedTransferEncodingHandler>()
            .AddHttpMessageHandler<AuthorizationHandler>()
            .AddHttpMessageHandler(provider => new RetryHandler(numberOfRetriesSelector.Invoke(provider.GetRequiredService<DriveApiConfig>())))
            ;

        if (useOfflinePolicy)
        {
            // We add the offline handler after the retry handler, so that it does see the retries.
            // We add the offline handler before the too many requests handler, so that it can see the responses.
            builder.AddHttpMessageHandler<OfflineHandler>();
        }

        return builder
            .AddHttpMessageHandler<TooManyRequestsHandler>()
            .AddHttpMessageHandler<CryptographyTimeProvisionHandler>()
            .AddTimeoutHandler(provider => timeoutSelector.Invoke(provider.GetRequiredService<DriveApiConfig>()));
    }
}
