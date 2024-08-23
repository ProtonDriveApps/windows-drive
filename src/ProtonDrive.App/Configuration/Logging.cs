using Microsoft.Extensions.Hosting;
using Serilog;

namespace ProtonDrive.App.Configuration;

public static class Logging
{
    public static IHostBuilder AddLogging(this IHostBuilder builder)
    {
        return builder
            .UseSerilog((context, loggerConfiguration) =>
                loggerConfiguration.ReadFrom.Configuration(context.Configuration));
    }
}
