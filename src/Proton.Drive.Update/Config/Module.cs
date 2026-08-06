using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proton.Drive.Shared.Diagnostics;
using Proton.Drive.Update.Updates;

namespace Proton.Drive.Update.Config;

/// <summary>
/// Initializes Update module and registers public interfaces.
/// </summary>
public static class Module
{
    public static IServiceCollection AddAppUpdate(this IServiceCollection services)
    {
        return services
            .AddSingleton(
                sp => new AppUpdater(
                    sp.GetRequiredService<ILoggerFactory>(),
                    sp.GetRequiredService<AppUpdateConfig>(),
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<IOsProcesses>()))
            .AddSingleton<AppUpdatesAutoCleanup>()

            .AddSingleton<IAppUpdateCleanup>(
                provider =>
                    new CleanableOnceAppUpdater(
                        new AsyncCleanableAppUpdater(
                            new SafeAppUpdater(
                                new LoggingAppUpdater(
                                    provider.GetRequiredService<ILogger<LoggingAppUpdater>>(),
                                    provider.GetRequiredService<AppUpdater>())))))

            .AddSingleton<INotifyingAppUpdate>(
                provider =>
                {
                    // Triggering automatic downloaded updates cleanup
                    provider.GetRequiredService<AppUpdatesAutoCleanup>();

                    return
                        new SafeNotifyingAppUpdateDecorator(
                            new LoggingNotifyingAppUpdateDecorator(
                                provider.GetRequiredService<ILogger<LoggingNotifyingAppUpdateDecorator>>(),
                                new ExtendedProgressAppUpdateDecorator(
                                    provider.GetRequiredService<AppUpdateConfig>().MinProgressDuration,
                                    new NotifyingAppUpdate(
                                        new AppUpdate(
                                            provider.GetRequiredService<AppUpdateConfig>().RolloutEligibilityThreshold,
                                            provider.GetRequiredService<AppUpdater>()),
                                        provider.GetRequiredService<ILogger<NotifyingAppUpdate>>()))));
                });
    }
}
