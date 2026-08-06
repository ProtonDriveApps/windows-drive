using Proton.Drive.Sdk.Sync.Client.Configuration;

namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class SdkHttpClientFactoryDecorator(IHttpClientFactory instanceToDecorate) : IHttpClientFactory
{
    private readonly IHttpClientFactory _decoratedInstance = instanceToDecorate;

    public HttpClient CreateClient(string name)
    {
        return _decoratedInstance.CreateClient(ApiClientConfigurator.SdkHttpClientName);
    }
}
