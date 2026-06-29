using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Proton.Drive.Shared.Net.Http.TlsPinning;

namespace Proton.Drive.Shared.Net.Http;

public static class HttpClientHandlerBuilderExtensions
{
    public static SocketsHttpHandler AddAutomaticDecompression(this SocketsHttpHandler handler)
    {
        handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli;
        return handler;
    }

    public static SocketsHttpHandler ConfigureCookies(this SocketsHttpHandler handler, IServiceProvider services)
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
    public static SocketsHttpHandler AddTlsPinning(this SocketsHttpHandler handler, string name, IServiceProvider services)
    {
        var tlsPinningHandlerFactory = services.GetRequiredService<TlsPinningHandlerFactory>();
        var tlsPinningHandler = tlsPinningHandlerFactory.CreateTlsPinningHandler(name);

        handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, chain, errors)
            => tlsPinningHandler.ValidateRemoteCertificate(name, certificate, chain, errors);

        return handler;
    }
}
