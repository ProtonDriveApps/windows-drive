using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace ProtonDrive.Shared.Net.Http;

public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Adds and configures an additional <see cref="TimeoutHandler"/> for a named <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHttpClientBuilder"/>.</param>
    /// <param name="timeout">The timeout value.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
    public static IHttpClientBuilder AddTimeoutHandler(this IHttpClientBuilder builder, TimeSpan timeout)
    {
        return builder.AddHttpMessageHandler(() => new TimeoutHandler(timeout));
    }

    /// <summary>
    /// Adds and configures an additional <see cref="TimeoutHandler"/> for a named <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHttpClientBuilder"/>.</param>
    /// <param name="configureTimeout">A delegate that is used to get a timeout value.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
    public static IHttpClientBuilder AddTimeoutHandler(this IHttpClientBuilder builder, Func<IServiceProvider, TimeSpan> configureTimeout)
    {
        return builder.AddHttpMessageHandler(provider => new TimeoutHandler(configureTimeout(provider)));
    }

    /// <summary>
    /// Adds a delegate that will be used to configure message handlers using <see cref="HttpMessageHandlerBuilder"/>
    /// for a named <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHttpClientBuilder"/>.</param>
    /// <param name="configureBuilder">A delegate that is used to configure an <see cref="HttpMessageHandlerBuilder"/>.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
    public static IHttpClientBuilder ConfigureHttpMessageHandlerBuilder(
        this IHttpClientBuilder builder,
        Action<HttpClientHandler, IServiceProvider> configureBuilder)
    {
        return builder.ConfigurePrimaryHttpMessageHandler((b, sp) => configureBuilder.Invoke((HttpClientHandler)b, sp));
    }
}
