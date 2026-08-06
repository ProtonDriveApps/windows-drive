using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Proton.Drive.App.Account;
using Proton.Drive.App.Docs;
using Proton.Drive.App.Drive.Services.SharedWithMe;
using Proton.Drive.App.EarlyAccess;
using Proton.Drive.App.InterProcessCommunication;
using Proton.Drive.App.Notifications;
using Proton.Drive.App.Notifications.Offers;
using Proton.Drive.App.Onboarding;
using Proton.Drive.App.Photos;
using Proton.Drive.App.Photos.Import;
using Proton.Drive.App.Photos.Volume;
using Proton.Drive.App.Reporting;
using Proton.Drive.App.Settings;
using Proton.Drive.App.Sync;
using Proton.Drive.App.Update;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Agent.Configuration;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Agent.Settings.Remote;
using Proton.Drive.Sdk.Sync.Agent.Volumes;
using Proton.Drive.Sdk.Sync.Client.Configuration;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Diagnostics;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.Net.Http;
using Proton.Drive.Shared.Offline;
using Proton.Drive.Shared.Repository;
using Proton.Drive.Update.Config;
using ClientNotification = Proton.Drive.App.Notifications.Contracts.Notification;

namespace Proton.Drive.App.Configuration;

public static class ServiceConfigurator
{
    public static readonly string CheckForUpdateHttpClientName = "CheckForUpdate";
    public static readonly string DownloadUpdateHttpClientName = "DownloadUpdate";

    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        return services
                .AddHostedService<HostedApp>()

                .AddAppUpdateConfig()
                .AddAppUpdate()

                .AddSyncAgentServices()

                .AddSingleton<IOsProcesses, SystemProcesses>()
                .AddSingleton<IMessenger, WeakReferenceMessenger>()

                .AddSingleton(
                    provider =>
                        new ClearingOnAccountSwitchingRepositoryDecorator<PhotoImportSettings>(
                            provider.GetRequiredService<IRepositoryFactory>()
                                .GetCachingRepository<PhotoImportSettings>("PhotoImportSettings.json")))
                .AddSingleton<IRepository<PhotoImportSettings>>(provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<PhotoImportSettings>>())
                .AddSingleton<IAccountSwitchingHandler>(provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<PhotoImportSettings>>())

                .AddSingleton(
                    provider =>
                        new ClearingOnAccountSwitchingRepositoryDecorator<OnboardingSettings>(
                            provider.GetRequiredService<IRepositoryFactory>()
                                .GetCachingRepository<OnboardingSettings>("OnboardingSettings.json")))
                .AddSingleton<IRepository<OnboardingSettings>>(
                    provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<OnboardingSettings>>())
                .AddSingleton<IAccountSwitchingHandler>(
                    provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<OnboardingSettings>>())

                .AddSingleton(
                    provider =>
                        new ClearingOnAccountSwitchingRepositoryDecorator<NotificationSettings>(
                            provider.GetRequiredService<IRepositoryFactory>()
                                .GetCachingRepository<NotificationSettings>("NotificationSettings.json")))
                .AddSingleton<IRepository<NotificationSettings>>(
                    provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<NotificationSettings>>())
                .AddSingleton<IAccountSwitchingHandler>(
                    provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<NotificationSettings>>())

                .AddSingleton<InstallationLogFilesCollector>()
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<InstallationLogFilesCollector>())

                .AddSingleton<ResilientSetup>()
                .AddSingleton<IPhotoVolumeStateAware>(provider => provider.GetRequiredService<ResilientSetup>())
                .AddSingleton<IOfflineStateAware>(provider => provider.GetRequiredService<ResilientSetup>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<ResilientSetup>())

                .AddSingleton<EarlyAccessService>()
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<EarlyAccessService>())
                .AddSingleton<IEarlyAccessService>(provider => provider.GetRequiredService<EarlyAccessService>())

                .AddSingleton<PhotoVolumeService>()
                .AddSingleton<IPhotoVolumeService>(provider => provider.GetRequiredService<PhotoVolumeService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<PhotoVolumeService>())
                .AddSingleton<IMainVolumeStateAware>(provider => provider.GetRequiredService<PhotoVolumeService>())
                .AddSingleton<IPhotosFeatureStateAware>(provider => provider.GetRequiredService<PhotoVolumeService>())

                .AddSingleton<PhotosFeatureService>()
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<PhotosFeatureService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<PhotosFeatureService>())
                .AddSingleton<IMainVolumeStateAware>(provider => provider.GetRequiredService<PhotosFeatureService>())
                .AddSingleton<IPhotoVolumeStateAware>(provider => provider.GetRequiredService<PhotosFeatureService>())
                .AddSingleton<IPhotosOnboardingStateAware>(provider => provider.GetRequiredService<PhotosFeatureService>())
                .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<PhotosFeatureService>())

                .AddSingleton<PhotoImportFolderService>()
                .AddSingleton<IPhotoImportFolderService>(provider => provider.GetRequiredService<PhotoImportFolderService>())
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<PhotoImportFolderService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<PhotoImportFolderService>())
                .AddSingleton<IAccountSwitchingAware>(provider => provider.GetRequiredService<PhotoImportFolderService>())

                .AddSingleton<OnboardingService>()
                .AddSingleton<IOnboardingService>(provider => provider.GetRequiredService<OnboardingService>())
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<OnboardingService>())
                .AddSingleton<IAccountSwitchingAware>(provider => provider.GetRequiredService<OnboardingService>())

                .AddSingleton<IPhotoImportEngineFactory, PhotoImportEngineFactory>()
                .AddSingleton<IPhotoAlbumNameProvider, PhotoAlbumNameProvider>()
                .AddSingleton<PhotoFileImporterFactory>()
                .AddSingleton<PhotoAlbumServiceFactory>()

                .AddSingleton<PhotoImportService>()
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<PhotoImportService>())
                .AddSingleton<IPhotoVolumeStateAware>(provider => provider.GetRequiredService<PhotoImportService>())
                .AddSingleton<IPhotoImportFoldersAware>(provider => provider.GetRequiredService<PhotoImportService>())

                .AddSingleton<SyncLifecycleController>()
                .AddSingleton<IOnboardingStateAware>(provider => provider.GetRequiredService<SyncLifecycleController>())

                .AddSingleton<IBugReportService, BugReportService>()

                .AddSingleton<IIpcMessageHandler, SyncRootPathsQueryHandler>()
                .AddSingleton<IIpcMessageHandler, RemoteIdsQueryHandler>()
                .AddSingleton<IIpcMessageHandler, AppActivationCommandHandler>()
                .AddSingleton<IIpcMessageHandler, OpenDocumentCommandHandler>()

                .AddSingleton<UpdateService>()
                .AddSingleton<IUpdateService>(provider => provider.GetRequiredService<UpdateService>())
                .AddSingleton<IEarlyAccessStateAware>(provider => provider.GetRequiredService<UpdateService>())
                .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<UpdateService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<UpdateService>())

                .AddSingleton<SharedWithMeDataService>()
                .AddSingleton<IAccountStateAware>(provider => provider.GetRequiredService<SharedWithMeDataService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<SharedWithMeDataService>())
                .AddSingleton<SharedWithMeDataItems>()
                .AddSingleton<ISharedWithMeDataProvider>(provider => provider.GetRequiredService<SharedWithMeDataItems>())
                .AddSingleton<SharedWithMeStateBasedUpdateDetector>()

                .AddSingleton(provider => new Lazy<IEnumerable<IEarlyAccessStateAware>>(provider.GetRequiredService<IEnumerable<IEarlyAccessStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IOnboardingStateAware>>(provider.GetRequiredService<IEnumerable<IOnboardingStateAware>>))

                .AddSingleton(provider => new Lazy<IEnumerable<ISharedWithMeOnboardingStateAware>>(provider.GetRequiredService<IEnumerable<ISharedWithMeOnboardingStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IPhotosOnboardingStateAware>>(provider.GetRequiredService<IEnumerable<IPhotosOnboardingStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IStorageOptimizationOnboardingStateAware>>(provider.GetRequiredService<IEnumerable<IStorageOptimizationOnboardingStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IPhotoImportFoldersAware>>(provider.GetRequiredService<IEnumerable<IPhotoImportFoldersAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IIpcMessageHandler>>(provider.GetRequiredService<IEnumerable<IIpcMessageHandler>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IOffersAware>>(provider.GetRequiredService<IEnumerable<IOffersAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IPhotosFeatureStateAware>>(provider.GetRequiredService<IEnumerable<IPhotosFeatureStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IPhotoVolumeStateAware>>(provider.GetRequiredService<IEnumerable<IPhotoVolumeStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IPhotoImportActivityAware>>(provider.GetRequiredService<IEnumerable<IPhotoImportActivityAware>>))

                .AddSingleton<DocumentOpener>()

                .AddSingleton<INotificationClient, NotificationClient>()
                .AddNotificationResources()

                .AddSingleton<OfferService>()
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<OfferService>())
                .AddSingleton<IAccountStateAware>(provider => provider.GetRequiredService<OfferService>())
                .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<OfferService>())
                .AddSingleton<IRemoteSettingsAware>(provider => provider.GetRequiredService<OfferService>())
                .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<OfferService>())

                .AddSingleton<ActivityService>()
                .AddSingleton<IAccountStateAware>(provider => provider.GetRequiredService<ActivityService>())
                .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<ActivityService>())
            ;
    }

    private static IServiceCollection AddNotificationResources(this IServiceCollection services)
    {
        services.AddSingleton(
            provider =>
            {
                var appConfig = provider.GetRequiredService<AppConfig>();
                var filePath = Path.Combine(appConfig.AppFolderPath, "Resources\\Notifications", "Notifications.json");

                return provider.GetRequiredService<IRepositoryFactory>()
                    .GetCachingCollectionRepository<ClientNotification>(filePath);
            });

        return services;
    }

    private static IServiceCollection AddAppUpdateConfig(this IServiceCollection services)
    {
        services
            .AddHttpClient(CheckForUpdateHttpClientName, ConfigureClient)
            .ApplyHttpClientPrimaryHandler(CheckForUpdateHttpClientName)
            .AddPolicyHandler(GetRetryPolicy)
            .AddTimeoutHandler(provider => provider.GetRequiredService<UpdateConfig>().Timeout);

        services
            .AddHttpClient(DownloadUpdateHttpClientName, ConfigureClient)
            .ApplyHttpClientPrimaryHandler(DownloadUpdateHttpClientName)
            .AddPolicyHandler(GetRetryPolicy)
            .AddTimeoutHandler(provider => provider.GetRequiredService<UpdateConfig>().Timeout);

        return services
            .AddSingleton(GetAppUpdateConfig);

        void ConfigureClient(HttpClient httpClient)
        {
            // Make sure the HttpClient does not interfere with the TimeoutPolicy
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
        }

        IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(IServiceProvider provider, HttpRequestMessage requestMessage)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutException>() // Thrown by TimeoutHandler if the inner call times out
                .WaitAndRetryAsync(
                    provider.GetRequiredService<UpdateConfig>().NumberOfRetries,
                    retryCount => TimeSpan.FromSeconds(Math.Pow(2.5, retryCount) / 4));
        }
    }

    private static AppUpdateConfig GetAppUpdateConfig(IServiceProvider provider)
    {
        var appArguments = provider.GetRequiredService<AppArguments>();
        var appConfig = provider.GetRequiredService<AppConfig>();
        var updateConfig = provider.GetRequiredService<UpdateConfig>();

        var clientInstanceSettings = provider.GetRequiredService<ClientInstanceSettings>();

        return new AppUpdateConfig(
            appArguments.LaunchMode,
            CheckForUpdateHttpClientName,
            DownloadUpdateHttpClientName,
            new Uri(updateConfig.UpdateUrl),
            clientInstanceSettings.RolloutEligibilityThreshold,
            appConfig.AppVersion,
            updateConfig.DownloadFolderPath,
            "EarlyAccess",
            updateConfig.MinProgressDuration,
            updateConfig.CleanupDelay);
    }
}
