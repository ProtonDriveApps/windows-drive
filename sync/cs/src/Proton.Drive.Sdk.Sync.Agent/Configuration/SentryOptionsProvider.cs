using Proton.Drive.Sdk.Sync.Client.Configuration;

namespace Proton.Drive.Sdk.Sync.Agent.Configuration;

internal sealed class SentryOptionsProvider
{
    private readonly DriveApiConfig _driveApiConfig;
    private readonly IErrorReportingHttpClientConfigurator _httpClientConfigurator;

    public SentryOptionsProvider(DriveApiConfig driveApiConfig, IErrorReportingHttpClientConfigurator httpClientConfigurator)
    {
        _driveApiConfig = driveApiConfig;
        _httpClientConfigurator = httpClientConfigurator;
    }

    public SentryOptions GetOptions()
    {
        ArgumentNullException.ThrowIfNull(_driveApiConfig.CoreBaseUrl);

        var baseUrlHost = _driveApiConfig.CoreBaseUrl.Host;

        var options = new SentryOptions
        {
            Dsn = $"https://f4db09bc4cc144dab7455dbd71231e7f@{baseUrlHost}/core/v4/reports/sentry/3",
            Release = _driveApiConfig.ClientVersion,
            Environment = "production",
            AttachStacktrace = true,
            SendClientReports = false,
            CreateHttpMessageHandler = _httpClientConfigurator.CreateHttpMessageHandler,
            ConfigureClient = _httpClientConfigurator.ConfigureHttpClient,
            AutoSessionTracking = false, // Disable Sentry's "Release Health" feature.
            IsGlobalModeEnabled = true, // Enabling this option is recommended for client applications only. It ensures all threads use the same global scope.
        };

        options.DisableDuplicateEventDetection();

#if DEBUG
        options.Environment = "debug";
#endif

        return options;
    }
}
