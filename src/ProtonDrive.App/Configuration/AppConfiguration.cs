using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ProtonDrive.App.Configuration;

public static class AppConfiguration
{
    private const string DefaultConfigFileName = "ProtonDrive.config.json";
    private const string CustomConfigFileName = "ProtonDrive.config.custom.json";

    public static IHostBuilder AddAppConfiguration(this IHostBuilder builder)
    {
        return builder
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();

                configuration
                    .AddJsonFile(DefaultConfigFileName, optional: false, reloadOnChange: false)
                    .AddInMemoryCollection(new AppRuntimeConfigurationSource())
                    .AddJsonFile(CustomConfigFileName, optional: true, reloadOnChange: false);
            });
    }
}
