using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Diagnostics;
using ProtonDrive.Update.Updates;

namespace ProtonDrive.Update.Config;

/// <summary>
/// Initializes Update module and registers public interfaces.
/// </summary>
public static class Module
{
    public static IServiceCollection AddAppUpdate(this IServiceCollection services, AppLaunchMode appLaunchMode)
    {
        return services
            .AddSingleton(
                sp => new AppUpdates(
                    sp.GetRequiredService<ILoggerFactory>(),
                    sp.GetRequiredService<AppUpdateConfig>(),
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<IOsProcesses>(),
                    appLaunchMode))
            .AddSingleton<AppUpdatesAutoCleanup>()

            .AddSingleton<IAppUpdates>(
                provider =>
                    new CleanableOnceAppUpdates(
                        new AsyncCleanableAppUpdates(
                            new SafeAppUpdates(
                                new LoggingAppUpdates(
                                    provider.GetRequiredService<ILogger<LoggingAppUpdates>>(),
                                    provider.GetRequiredService<AppUpdates>())))))

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
                                            provider.GetRequiredService<AppUpdates>()),
                                        provider.GetRequiredService<ILogger<NotifyingAppUpdate>>()))));
                });
    }
}
