namespace Proton.Drive.Sdk.Sync.Client.Configuration;

public interface IErrorReportingHttpClientConfigurator
{
    HttpMessageHandler CreateHttpMessageHandler();

    void ConfigureHttpClient(HttpClient httpClient);
}
