using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Net.Http.TlsPinning;

namespace ProtonDrive.Shared.Net.Http;

public static class HttpClientHandlerBuilderExtensions
{
    public static HttpClientHandler AddAutomaticDecompression(this HttpClientHandler handler)
    {
        handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli;
        return handler;
    }

    public static HttpClientHandler ConfigureCookies(this HttpClientHandler handler, IServiceProvider services)
    {
        handler.CookieContainer = services.GetRequiredService<CookieContainer>();
        return handler;
    }

    /// <summary>
    /// Configures the <see cref="HttpClientHandler"></see> to apply server certificate public key pinning for a named <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="handler">The <see cref="HttpClientHandler"/>.</param>
    /// <param name="name">The name of the HTTP client that will use this handler.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
    public static HttpClientHandler AddTlsPinning(this HttpClientHandler handler, string name, IServiceProvider services)
    {
        var tlsPinningHandler = new TlsPinningHandler(
            ConfigureTlsPinning(name, services),
            services.GetRequiredService<Func<ITlsPinningReportClient>>());

        handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, errors) =>
            tlsPinningHandler.ValidateRemoteCertificate(sender.RequestUri?.Host ?? string.Empty, certificate, chain, errors);

        return handler;
    }

    private static TlsPinningConfig ConfigureTlsPinning(string clientName, IServiceProvider provider)
    {
        // If TLS pinning is globally not enabled then ignore the TLS pinning configuration
        var appConfig = provider.GetRequiredService<AppConfig>();
        if (!appConfig.TlsPinningEnabled)
        {
            return appConfig.RemoteCertificateErrorsIgnored
                ? TlsPinningConfig.DisabledAndRemoteCertificateErrorsIgnored()
                : TlsPinningConfig.Disabled();
        }

        var namedConfigs = provider.GetRequiredService<IReadOnlyDictionary<string, TlsPinningConfig>>();
        if (namedConfigs.TryGetValue(clientName, out var namedConfig))
        {
            return namedConfig;
        }

        // If there is no named TLS pinning config section, the "Default" section is used
        if (namedConfigs.TryGetValue("Default", out var defaultConfig))
        {
            return defaultConfig;
        }

        provider.GetRequiredService<ILogger<TlsPinningConfig>>().LogError("TLS pinning configuration is missing for \"{Name}\"", clientName);

        // Prevent the communication
        return TlsPinningConfig.Blocking();
    }
}
