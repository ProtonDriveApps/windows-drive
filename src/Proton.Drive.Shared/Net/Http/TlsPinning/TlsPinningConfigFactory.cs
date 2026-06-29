using Microsoft.Extensions.Logging;
using Proton.Drive.Shared.Configuration;

namespace Proton.Drive.Shared.Net.Http.TlsPinning;

public sealed class TlsPinningConfigFactory
{
    private readonly IReadOnlyDictionary<string, TlsPinningConfig> _namedConfigs;
    private readonly AppConfig _appConfig;
    private readonly ILogger<TlsPinningConfigFactory> _logger;

    public TlsPinningConfigFactory(IReadOnlyDictionary<string, TlsPinningConfig> namedConfigs, AppConfig appConfig, ILogger<TlsPinningConfigFactory> logger)
    {
        _namedConfigs = namedConfigs;
        _appConfig = appConfig;
        _logger = logger;
    }

    public TlsPinningConfig CreateTlsPinningConfig(string clientName)
    {
        if (!_appConfig.TlsPinningEnabled)
        {
            return _appConfig.RemoteCertificateErrorsIgnored
                ? TlsPinningConfig.DisabledAndRemoteCertificateErrorsIgnored()
                : TlsPinningConfig.Disabled();
        }

        if (_namedConfigs.TryGetValue(clientName, out var namedConfig))
        {
            return namedConfig;
        }

        // If there is no named TLS pinning config section, the "Default" section is used
        if (_namedConfigs.TryGetValue("Default", out var defaultConfig))
        {
            return defaultConfig;
        }

        _logger.LogError("TLS pinning configuration is missing for \"{Name}\"", clientName);

        // Prevent the communication
        return TlsPinningConfig.Blocking();
    }
}
