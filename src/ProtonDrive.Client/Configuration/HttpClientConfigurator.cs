using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using ProtonDrive.Client.Authentication;
using ProtonDrive.Client.Offline;
using ProtonDrive.Shared.Net.Http;

namespace ProtonDrive.Client.Configuration;

internal static class HttpClientConfigurator
{
    public static IHttpClientBuilder ConfigureHttpClient(
        this IHttpClientBuilder builder,
        string name,
        Func<DriveApiConfig, int> numberOfRetriesSelector,
        Func<DriveApiConfig, TimeSpan> timeoutSelector,
        bool useOfflinePolicy = true)
    {
        builder
            .ConfigureHttpMessageHandlerBuilder(
                (httpClientHandler, serviceProvider) =>
                {
                    httpClientHandler
                        .AddAutomaticDecompression()
                        .ConfigureCookies(serviceProvider)
                        .AddTlsPinning(name, serviceProvider);
                })
            .AddHttpMessageHandler<ServerTimeRecordingHandler>()
            .AddHttpMessageHandler<TooManyRequestsHandler>()
            .AddHttpMessageHandler<ChunkedTransferEncodingHandler>()
            .AddHttpMessageHandler<AuthorizationHandler>();

        if (useOfflinePolicy)
        {
            // We add the offline handler before the retry policy handler, so that it does not see the retries.
            // This way, it does not enable the offline mode before all retries are finished.
            builder.AddHttpMessageHandler<OfflineHandler>();
        }

        return builder.AddPolicyHandler((provider, _) => GetRetryPolicy(provider, numberOfRetriesSelector))
            .AddTimeoutHandler(provider => timeoutSelector.Invoke(provider.GetRequiredService<DriveApiConfig>()));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(IServiceProvider provider, Func<DriveApiConfig, int> numberOfRetriesSelector)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutException>() // Thrown by TimeoutHandler if the inner call times out
            .WaitAndRetryAsync(
                numberOfRetriesSelector.Invoke(provider.GetRequiredService<DriveApiConfig>()),
                retryCount => TimeSpan.FromSeconds(Math.Pow(2.5, retryCount) / 4));
    }
}
