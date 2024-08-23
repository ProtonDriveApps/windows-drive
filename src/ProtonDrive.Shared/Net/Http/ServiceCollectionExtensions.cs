using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;

namespace ProtonDrive.Shared.Net.Http;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces default logging in the <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="services">A collection of service descriptors.</param>
    /// <returns>A collection of service descriptors.</returns>
    public static IServiceCollection ReplaceHttpClientLogging(this IServiceCollection services)
    {
        return services
            .RemoveAll<IHttpMessageHandlerBuilderFilter>()
            .AddSingleton<IHttpMessageHandlerBuilderFilter, LoggingHttpMessageHandlerBuilderFilter>();
    }
}
