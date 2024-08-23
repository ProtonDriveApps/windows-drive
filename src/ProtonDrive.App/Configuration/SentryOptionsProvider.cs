using System;
using System.Reflection;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Net.Http;
using Sentry;

namespace ProtonDrive.App.Configuration;

public static class SentryOptionsProvider
{
    public static SentryOptions GetOptions(Func<IServiceProvider?> getServiceProvider)
    {
        var options = new SentryOptions
        {
            Dsn = new Dsn("https://f4db09bc4cc144dab7455dbd71231e7f@drive-api.proton.me/core/v4/reports/sentry/3"),
            Release = GetRelease("windows-drive@{AppVersion}"),
            AttachStacktrace = true,
            ConfigureHandler = (handler, _) =>
            {
                var serviceProvider = getServiceProvider.Invoke();
                if (serviceProvider is not null)
                {
                    handler
                        .AddAutomaticDecompression()
                        .ConfigureCookies(serviceProvider)
                        .AddTlsPinning("ErrorReport", serviceProvider);
                }
            },
            Environment = "production",
        };

#if DEBUG
        options.Environment = "debug";
#endif

        return options;
    }

    private static string GetRelease(string clientRelease)
    {
        return clientRelease.Replace("{AppVersion}", AppVersion());
    }

    private static string AppVersion()
    {
        // Normalized app version
        return Assembly.GetExecutingAssembly().GetName().Version?.ToNormalized().ToString() ?? string.Empty;
    }
}
