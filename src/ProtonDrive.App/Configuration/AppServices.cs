using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using ProtonDrive.App.Account;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Devices;
using ProtonDrive.App.Docs;
using ProtonDrive.App.Drive.Services;
using ProtonDrive.App.Drive.Services.Events;
using ProtonDrive.App.Drive.Services.SharedWithMe;
using ProtonDrive.App.EarlyAccess;
using ProtonDrive.App.Features;
using ProtonDrive.App.FileSystem.Local;
using ProtonDrive.App.FileSystem.Remote;
using ProtonDrive.App.Health;
using ProtonDrive.App.Instrumentation.Observability;
using ProtonDrive.App.Instrumentation.Observability.TransferPerformance;
using ProtonDrive.App.Instrumentation.Telemetry;
using ProtonDrive.App.Instrumentation.Telemetry.FileIntegrity;
using ProtonDrive.App.Instrumentation.Telemetry.MappingSetup;
using ProtonDrive.App.Instrumentation.Telemetry.Synchronization;
using ProtonDrive.App.Instrumentation.Telemetry.ThumbnailGeneration;
using ProtonDrive.App.InterProcessCommunication;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.Setup;
using ProtonDrive.App.Mapping.Setup.CloudFiles;
using ProtonDrive.App.Mapping.Setup.ForeignDevices;
using ProtonDrive.App.Mapping.Setup.HostDeviceFolders;
using ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeItem;
using ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeRootFolder;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.Mapping.Teardown;
using ProtonDrive.App.Notifications;
using ProtonDrive.App.Notifications.Offers;
using ProtonDrive.App.Onboarding;
using ProtonDrive.App.Photos;
using ProtonDrive.App.Photos.Import;
using ProtonDrive.App.Photos.Volume;
using ProtonDrive.App.Reporting;
using ProtonDrive.App.Services;
using ProtonDrive.App.Settings;
using ProtonDrive.App.Settings.Remote;
using ProtonDrive.App.Sync;
using ProtonDrive.App.Update;
using ProtonDrive.App.Volumes;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Caching;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Devices;
using ProtonDrive.Shared.Diagnostics;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Features;
using ProtonDrive.Shared.HumanVerification;
using ProtonDrive.Shared.Net.Http;
using ProtonDrive.Shared.Net.Http.TlsPinning;
using ProtonDrive.Shared.Offline;
using ProtonDrive.Shared.Reporting;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Sync.Shared.Diagnostics.Metrics.Thumbnails;
using ProtonDrive.Sync.Shared.FileSystem.Integration;
using ProtonDrive.Sync.Shared.FileSystem.Metadata.GoogleTakeout;
using ProtonDrive.Update.Config;
using ClientNotification = ProtonDrive.App.Notifications.Contracts.Notification;

namespace ProtonDrive.App.Configuration;

public static class AppServices
{
    public static readonly string CheckForUpdateHttpClientName = "CheckForUpdate";
    public static readonly string DownloadUpdateHttpClientName = "DownloadUpdate";

    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        return services
                .AddHostedService<HostedApp>()
                .AddSingleton<SentryOptionsProvider>()
                .AddSingleton<IErrorReporting, ErrorReporting>()

                .AddSingleton(
                    provider => provider.GetRequiredService<IConfiguration>().Get<AppConfig>(options => options.BindNonPublicProperties = true) ??
                        throw new InvalidOperationException("Failed to obtain app configuration"))
                .AddSingleton(
                    provider => provider.GetRequiredService<IConfiguration>().GetSection("Urls").Get<UrlConfig>(options => options.BindNonPublicProperties = true) ??
                        throw new InvalidOperationException("Failed to obtain app URL configuration"))
                .AddSingleton(
                    provider => provider.GetRequiredService<IConfiguration>().GetSection("Update").Get<UpdateConfig>(options => options.BindNonPublicProperties = true) ??
                        throw new InvalidOperationException("Failed to obtain app update configuration"))
                .AddSingleton(
                    provider => provider.GetRequiredService<IConfiguration>().GetSection("TlsPinning").Get<IReadOnlyDictionary<string, TlsPinningConfig>>(options => options.BindNonPublicProperties = true) ??
                        throw new InvalidOperationException("Failed to obtain TLS pinning configuration"))
                .AddSingleton(
                    provider => provider.GetRequiredService<IConfiguration>().GetSection("FeatureManagement").Get<LocalFeatureFlags>(options => options.BindNonPublicProperties = true) ??
                        throw new InvalidOperationException("Failed to obtain app feature configuration"))

                .AddSingleton(GetDriveApiConfig)
                .AddApiClients()
                .AddFileSystemClient()

                .AddSingleton<TooManyRequestsBlockedEndpoints>()
                .AddTransient<TooManyRequestsHandler>()
                .AddTransient<HumanVerificationHandler>()

                .AddAppUpdateConfig()
                .AddAppUpdate()

                .ReplaceHttpClientLogging()

                .AddSingleton<IClock, SystemClock>()
                .AddSingleton<IOsProcesses, SystemProcesses>()
                .AddSingleton<IMessenger, WeakReferenceMessenger>()

                .AddSingleton<Func<TimeSpan, IPeriodicTimer>>(_ => (period) => new DefaultPeriodicTimer(period))

                .AddSingleton<IClearableMemoryCache>(new ClearableMemoryCache(new MemoryCache(new MemoryCacheOptions())))
                .AddSingleton<IMemoryCache>(provider => provider.GetRequiredService<IClearableMemoryCache>())

                .AddSingleton<IRemoteFolderService, RemoteFolderService>()
                .AddSingleton<INumberSuffixedNameGenerator, NumberSuffixedNameGenerator>()

                .AddSingleton<IRepositoryFactory, RepositoryFactory>()
                .AddSingleton(
                    provider => provider.GetRequiredService<IRepositoryFactory>()
                        .GetRepository<MappingSettings>(AppRuntimeConfigurationSource.SyncFoldersMappingFilename))

                .AddSingleton(
                    provider =>
                        new ClearingOnAccountSwitchingRepositoryDecorator<IReadOnlyDictionary<Feature, bool>>(
                            provider.GetRequiredService<IRepositoryFactory>()
                                .GetCachingRepository<IReadOnlyDictionary<Feature, bool>>("FeatureFlagSettings.json")))
                .AddSingleton<IRepository<IReadOnlyDictionary<Feature, bool>>>(
                    provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<IReadOnlyDictionary<Feature, bool>>>())
                .AddSingleton<IAccountSwitchingHandler>(
                    provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<IReadOnlyDictionary<Feature, bool>>>())

                .AddSingleton(
                    provider =>
                        new ClearingOnAccountSwitchingRepositoryDecorator<SyncSettings>(
                            provider.GetRequiredService<IRepositoryFactory>()
                                .GetCachingRepository<SyncSettings>("SyncSettings.json")))
                .AddSingleton<IRepository<SyncSettings>>(provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<SyncSettings>>())
                .AddSingleton<IAccountSwitchingHandler>(provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<SyncSettings>>())

                .AddSingleton(
                    provider =>
                        new ClearingOnAccountSwitchingRepositoryDecorator<PhotoImportSettings>(
                            provider.GetRequiredService<IRepositoryFactory>()
                                .GetCachingRepository<PhotoImportSettings>("PhotoImportSettings.json")))
                .AddSingleton<IRepository<PhotoImportSettings>>(provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<PhotoImportSettings>>())
                .AddSingleton<IAccountSwitchingHandler>(provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<PhotoImportSettings>>())

                .AddSingleton(
                    provider =>
                        new ClearingOnAccountSwitchingRepositoryDecorator<DeviceSettings>(
                            provider.GetRequiredService<IRepositoryFactory>()
                                .GetCachingRepository<DeviceSettings>("DeviceSettings.json")))
                .AddSingleton<IRepository<DeviceSettings>>(
                    provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<DeviceSettings>>())
                .AddSingleton<IAccountSwitchingHandler>(
                    provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<DeviceSettings>>())

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

                .AddSingleton(
                    provider =>
                        new ClearingOnAccountSwitchingRepositoryDecorator<FileConsistencyGuardSettings>(
                            provider.GetRequiredService<IRepositoryFactory>()
                                .GetCachingRepository<FileConsistencyGuardSettings>("FileConsistencyGuardSettings.json")))
                .AddSingleton<IRepository<FileConsistencyGuardSettings>>(
                    provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<FileConsistencyGuardSettings>>())
                .AddSingleton<IAccountSwitchingHandler>(
                    provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<FileConsistencyGuardSettings>>())

                .AddSingleton(
                    provider => provider.GetRequiredService<IRepositoryFactory>()
                        .GetCachingRepository<UserSettings>("UserSettings.json"))

                .AddSingleton<ClientInstanceSettings>()
                .AddSingleton<IClientInstanceIdentityProvider, ClientInstanceIdentityProvider>()

                .AddSingleton<StatefulSessionService>()
                .AddSingleton<IStatefulSessionService>(provider => provider.GetRequiredService<StatefulSessionService>())
                .AddSingleton<IAuthenticationService>(provider => provider.GetRequiredService<StatefulSessionService>())
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<StatefulSessionService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<StatefulSessionService>())
                .AddSingleton<IMainVolumeStateAware>(provider => provider.GetRequiredService<StatefulSessionService>())
                .AddSingleton<IAccountStateAware>(provider => provider.GetRequiredService<StatefulSessionService>())

                .AddSingleton<InstallationLogFilesCollector>()
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<InstallationLogFilesCollector>())

                .AddSingleton<EarlyAccessService>()
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<EarlyAccessService>())
                .AddSingleton<IEarlyAccessService>(provider => provider.GetRequiredService<EarlyAccessService>())

                .AddSingleton<RemoteSettingsService>()
                .AddSingleton<IRemoteSettingsService>(provider => provider.GetRequiredService<RemoteSettingsService>())
                .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<RemoteSettingsService>())

                .AddSingleton<FeatureService>()
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<FeatureService>())
                .AddSingleton<IAccountSwitchingAware>(provider => provider.GetRequiredService<FeatureService>())
                .AddSingleton<IAccountStateAware>(provider => provider.GetRequiredService<FeatureService>())
                .AddSingleton<IFeatureFlagProvider>(provider => provider.GetRequiredService<FeatureService>())

                .AddSingleton<AccountService>()
                .AddSingleton<IAccountService>(provider => provider.GetRequiredService<AccountService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<AccountService>())
                .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<AccountService>())

                .AddSingleton<IAccountSwitchingService, AccountSwitchingService>()

                .AddSingleton<UserService>()
                .AddSingleton<IUserService>(provider => provider.GetRequiredService<UserService>())

                .AddSingleton<IActiveVolumeService, ActiveVolumeService>()
                .AddSingleton<MainVolumeService>()
                .AddSingleton<IMainVolumeService>(provider => provider.GetRequiredService<MainVolumeService>())
                .AddSingleton<IAccountStateAware>(provider => provider.GetRequiredService<MainVolumeService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<MainVolumeService>())

                .AddSingleton<PhotoVolumeService>()
                .AddSingleton<IPhotoVolumeService>(provider => provider.GetRequiredService<PhotoVolumeService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<PhotoVolumeService>())
                .AddSingleton<IMainVolumeStateAware>(provider => provider.GetRequiredService<PhotoVolumeService>())
                .AddSingleton<IPhotosFeatureStateAware>(provider => provider.GetRequiredService<PhotoVolumeService>())

                .AddSingleton<DeviceService>()
                .AddSingleton<IDeviceService>(provider => provider.GetRequiredService<DeviceService>())
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<DeviceService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<DeviceService>())
                .AddSingleton<IMainVolumeStateAware>(provider => provider.GetRequiredService<DeviceService>())
                .AddSingleton<IRemoteDeviceEventsAware>(provider => provider.GetRequiredService<DeviceService>())
                .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<DeviceService>())

                .AddSingleton<PhotosFeatureService>()
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<PhotosFeatureService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<PhotosFeatureService>())
                .AddSingleton<IMainVolumeStateAware>(provider => provider.GetRequiredService<PhotosFeatureService>())
                .AddSingleton<IPhotoVolumeStateAware>(provider => provider.GetRequiredService<PhotosFeatureService>())
                .AddSingleton<IPhotosOnboardingStateAware>(provider => provider.GetRequiredService<PhotosFeatureService>())
                .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<PhotosFeatureService>())

                .AddSingleton<SyncFolderService>()
                .AddSingleton<ISyncFolderService>(provider => provider.GetRequiredService<SyncFolderService>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<SyncFolderService>())

                .AddSingleton<SyncFolderProvider>()
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<SyncFolderProvider>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<SyncFolderProvider>())
                .AddSingleton<IMappingStateAware>(provider => provider.GetRequiredService<SyncFolderProvider>())

                .AddSingleton<PhotoImportFolderService>()
                .AddSingleton<IPhotoImportFolderService>(provider => provider.GetRequiredService<PhotoImportFolderService>())
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<PhotoImportFolderService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<PhotoImportFolderService>())
                .AddSingleton<IAccountSwitchingAware>(provider => provider.GetRequiredService<PhotoImportFolderService>())

                .AddSingleton<MappingRegistry>()
                .AddSingleton<IMappingRegistry>(provider => provider.GetRequiredService<MappingRegistry>())
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<MappingRegistry>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<MappingRegistry>())

                .AddSingleton<MappingSetupService>()
                .AddSingleton<IMappingSetupService>(provider => provider.GetRequiredService<MappingSetupService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<MappingSetupService>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<MappingSetupService>())
                .AddSingleton<IMainVolumeStateAware>(provider => provider.GetRequiredService<MappingSetupService>())
                .AddSingleton<ISyncStateAware>(provider => provider.GetRequiredService<MappingSetupService>())
                .AddSingleton<IOnboardingStateAware>(provider => provider.GetRequiredService<MappingSetupService>())
                .AddSingleton<IRootDeletionHandler>(provider => provider.GetRequiredService<MappingSetupService>())

                .AddSingleton<IMappingSetupPipeline, MappingSetupPipeline>()
                .AddSingleton<MappingValidationDispatcher>()
                .AddSingleton<MappingFoldersSetupDispatcher>()
                .AddSingleton<MappingSetupFinalizationDispatcher>()

                .AddSingleton<CloudFilesMappingFolderValidationStep>()
                .AddSingleton<CloudFilesMappingFoldersSetupStep>()
                .AddSingleton<CloudFilesMappingSetupFinalizationStep>()

                .AddSingleton<HostDeviceFolderMappingFolderValidationStep>()
                .AddSingleton<HostDeviceFolderMappingFoldersSetupStep>()
                .AddSingleton<HostDeviceFolderMappingSetupFinalizationStep>()

                .AddSingleton<ForeignDeviceMappingFolderValidationStep>()
                .AddSingleton<ForeignDeviceMappingFoldersSetupStep>()
                .AddSingleton<ForeignDeviceMappingSetupFinalizationStep>()

                .AddSingleton<SharedWithMeRootFolderMappingFoldersSetupStep>()
                .AddSingleton<SharedWithMeRootFolderMappingSetupFinalizationStep>()
                .AddSingleton<SharedWithMeItemMappingValidationStep>()
                .AddSingleton<SharedWithMeItemMappingSetupStep>()
                .AddSingleton<SharedWithMeItemMappingSetupFinalizationStep>()

                .AddSingleton<ILocalFolderValidationStep, LocalFolderValidationStep>()
                .AddSingleton<ILocalSyncFolderValidator, LocalSyncFolderValidator>()
                .AddSingleton<ILocalStorageOptimizationStep, LocalStorageOptimizationStep>()
                .AddSingleton<OnDemandSyncEligibilityValidator>()
                .AddSingleton<IRemoteFolderValidationStep, RemoteFolderValidationStep>()
                .AddSingleton<IRemoteSharedWithMeItemValidationStep, RemoteSharedWithMeItemValidationStep>()

                .AddSingleton<LocalFolderSetupAssistant>()
                .AddSingleton<ILocalFolderSetupAssistant>(provider => provider.GetRequiredService<LocalFolderSetupAssistant>())

                .AddSingleton<IMappingTeardownPipeline, MappingTeardownPipeline>()
                .AddSingleton<CloudFilesMappingTeardownStep>()
                .AddSingleton<HostDeviceFolderMappingTeardownStep>()
                .AddSingleton<ForeignDeviceMappingTeardownStep>()
                .AddSingleton<SharedWithMeItemMappingTeardownStep>()
                .AddSingleton<SharedWithMeRootFolderMappingTeardownStep>()
                .AddSingleton<ILocalSpecialSubfoldersDeletionStep, LocalSpecialFoldersDeletionStep>()

                .AddSingleton<VolumeIdentityProvider>()
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<VolumeIdentityProvider>())

                .AddSingleton<LocalFolderIdentityValidator>()
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<LocalFolderIdentityValidator>())

                .AddSingleton<LocalFolderDivergedIdentityHandler>()

                .AddSingleton<RemoteFolderNameValidator>()
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<RemoteFolderNameValidator>())

                .AddSingleton<MappingClearingService>()
                .AddSingleton<IAccountSwitchingHandler>(provider => provider.GetRequiredService<MappingClearingService>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<MappingClearingService>())

                .AddSingleton<DeviceMappingMaintenanceService>()
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<DeviceMappingMaintenanceService>())
                .AddSingleton<IDeviceServiceStateAware>(provider => provider.GetRequiredService<DeviceMappingMaintenanceService>())
                .AddSingleton<IDevicesAware>(provider => provider.GetRequiredService<DeviceMappingMaintenanceService>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<DeviceMappingMaintenanceService>())

                .AddSingleton<SharedWithMeMappingService>()
                .AddSingleton<ISharedWithMeMappingService>(provider => provider.GetRequiredService<SharedWithMeMappingService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<SharedWithMeMappingService>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<SharedWithMeMappingService>())

                .AddSingleton<SyncFolderPathProvider>()
                .AddSingleton<ISyncFolderPathProvider>(provider => provider.GetRequiredService<SyncFolderPathProvider>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<SyncFolderPathProvider>())

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

                .AddSingleton<SyncService>()
                .AddSingleton<ISyncService>(provider => provider.GetRequiredService<SyncService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<SyncService>())
                .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<SyncService>())
                .AddSingleton<IMappingsSetupStateAware>(provider => provider.GetRequiredService<SyncService>())
                .AddSingleton<IOfflineStateAware>(provider => provider.GetRequiredService<SyncService>())
                .AddSingleton<IMappedFileSystemIdentityProvider>(provider => provider.GetRequiredService<SyncService>())
                .AddSingleton<ISyncRootPathProvider>(provider => provider.GetRequiredService<SyncService>())

                .AddSingleton<SyncStateClearingService>()
                .AddSingleton<IAccountSwitchingHandler>(provider => provider.GetRequiredService<SyncStateClearingService>())

                .AddSingleton<RemoteIdsFromLocalPathProvider>()
                .AddSingleton<IRemoteIdsFromLocalPathProvider>(provider => provider.GetRequiredService<RemoteIdsFromLocalPathProvider>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<RemoteIdsFromLocalPathProvider>())

                .AddSingleton<RemoteIdsFromNodeIdProvider>()
                .AddSingleton<IRemoteIdsFromNodeIdProvider>(provider => provider.GetRequiredService<RemoteIdsFromNodeIdProvider>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<RemoteIdsFromNodeIdProvider>())

                .AddSingleton<ResilientSetup>()
                .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<ResilientSetup>())
                .AddSingleton<IRemoteSettingsStateAware>(provider => provider.GetRequiredService<ResilientSetup>())
                .AddSingleton<IAccountStateAware>(provider => provider.GetRequiredService<ResilientSetup>())
                .AddSingleton<IMainVolumeStateAware>(provider => provider.GetRequiredService<ResilientSetup>())
                .AddSingleton<IPhotoVolumeStateAware>(provider => provider.GetRequiredService<ResilientSetup>())
                .AddSingleton<IDeviceServiceStateAware>(provider => provider.GetRequiredService<ResilientSetup>())
                .AddSingleton<IMappingsSetupStateAware>(provider => provider.GetRequiredService<ResilientSetup>())
                .AddSingleton<IOfflineStateAware>(provider => provider.GetRequiredService<ResilientSetup>())
                .AddSingleton<ISyncStateAware>(provider => provider.GetRequiredService<ResilientSetup>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<ResilientSetup>())

                .AddSingleton<ISwitchingToVolumeEventsHandler, SwitchingToVolumeEventsHandler>()
                .AddSingleton<LocalRootMapForDeletionDetectionFactory>()
                .AddSingleton<RemoteDecoratedFileSystemClientFactory>()
                .AddSingleton<RemoteDecoratedEventLogClientFactory>()

                .AddSingleton<SyncAgentFactory>()

                .AddSingleton<IFileConsistencyGuardFactory, FileConsistencyGuardFactory>()
                .AddSingleton<FileConsistencyGuardStatusReporter>()
                .AddSingleton<FileConsistencyGuardApplicabilityVerifier>()

                .AddSingleton<RemoteRootMapForDeletionDetectionFactory>()

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

                .AddSingleton<CoreEventService>()
                .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<CoreEventService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<CoreEventService>())

                .AddSingleton<UserStateChangeHandler>()

                .AddSingleton<SharedWithMeDataService>()
                .AddSingleton<IAccountStateAware>(provider => provider.GetRequiredService<SharedWithMeDataService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<SharedWithMeDataService>())
                .AddSingleton<SharedWithMeDataItems>()
                .AddSingleton<ISharedWithMeDataProvider>(provider => provider.GetRequiredService<SharedWithMeDataItems>())
                .AddSingleton<SharedWithMeStateBasedUpdateDetector>()

                .AddSingleton<TelemetryService>()
                .AddSingleton<IRemoteSettingsAware>(provider => provider.GetRequiredService<TelemetryService>())
                .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<TelemetryService>())

                .AddSingleton<IThumbnailGenerationMetricsCollector, ThumbnailGenerationMetricsCollector>()
                .AddSingleton<ThumbnailGenerationReportingService>()
                .AddSingleton<IRemoteSettingsAware>(provider => provider.GetRequiredService<ThumbnailGenerationReportingService>())

                .AddSingleton<FileIntegrityStatistics>()
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<FileIntegrityStatistics>())

                .AddSingleton<TransferPerformanceMonitors>()

                .AddSingleton<TransferPerformanceMeter>()
                .AddSingleton<ISyncActivityAware>(provider => provider.GetRequiredService<TransferPerformanceMeter>())
                .AddSingleton<IAccountSwitchingAware>(provider => provider.GetRequiredService<TransferPerformanceMeter>())

                .AddSingleton<GenericTransferPerformanceMetricsFactory>()

                .AddSingleton<ObservabilityService>()
                .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<ObservabilityService>())
                .AddSingleton<IRemoteSettingsAware>(provider => provider.GetRequiredService<ObservabilityService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<ObservabilityService>())

                .AddSingleton<SyncedItemCounters>()
                .AddSingleton<SharedWithMeItemCounters>()
                .AddSingleton<OpenedDocumentsCounters>()

                .AddSingleton<MappingSetupStatistics>()
                .AddSingleton<IMappingStateAware>(provider => provider.GetRequiredService<MappingSetupStatistics>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<MappingSetupStatistics>())

                .AddSingleton<SyncStatistics>()
                .AddSingleton<ISyncStateAware>(provider => provider.GetRequiredService<SyncStatistics>())
                .AddSingleton<ISyncActivityAware>(provider => provider.GetRequiredService<SyncStatistics>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<SyncStatistics>())

                .AddSingleton<ErrorCounter>()
                .AddSingleton<IErrorCounter>(provider => provider.GetRequiredService<ErrorCounter>())
                .AddSingleton<IErrorCountProvider>(provider => provider.GetRequiredService<ErrorCounter>())

                .AddSingleton(provider => new Lazy<IEnumerable<ISessionStateAware>>(provider.GetRequiredService<IEnumerable<ISessionStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IRemoteSettingsStateAware>>(provider.GetRequiredService<IEnumerable<IRemoteSettingsStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IRemoteSettingsAware>>(provider.GetRequiredService<IEnumerable<IRemoteSettingsAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IEarlyAccessStateAware>>(provider.GetRequiredService<IEnumerable<IEarlyAccessStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IAccountStateAware>>(provider.GetRequiredService<IEnumerable<IAccountStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IAccountSwitchingAware>>(provider.GetRequiredService<IEnumerable<IAccountSwitchingAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IUserStateAware>>(provider.GetRequiredService<IEnumerable<IUserStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IMainVolumeStateAware>>(provider.GetRequiredService<IEnumerable<IMainVolumeStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IPhotoVolumeStateAware>>(provider.GetRequiredService<IEnumerable<IPhotoVolumeStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IDeviceServiceStateAware>>(provider.GetRequiredService<IEnumerable<IDeviceServiceStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IDevicesAware>>(provider.GetRequiredService<IEnumerable<IDevicesAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IMappingsAware>>(provider.GetRequiredService<IEnumerable<IMappingsAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IMappingStateAware>>(provider.GetRequiredService<IEnumerable<IMappingStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IOnboardingStateAware>>(provider.GetRequiredService<IEnumerable<IOnboardingStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<ISharedWithMeOnboardingStateAware>>(provider.GetRequiredService<IEnumerable<ISharedWithMeOnboardingStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IPhotosOnboardingStateAware>>(provider.GetRequiredService<IEnumerable<IPhotosOnboardingStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IStorageOptimizationOnboardingStateAware>>(provider.GetRequiredService<IEnumerable<IStorageOptimizationOnboardingStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IMappingsSetupStateAware>>(provider.GetRequiredService<IEnumerable<IMappingsSetupStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IAccountSwitchingHandler>>(provider.GetRequiredService<IEnumerable<IAccountSwitchingHandler>>))
                .AddSingleton(provider => new Lazy<IEnumerable<ISyncFoldersAware>>(provider.GetRequiredService<IEnumerable<ISyncFoldersAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IPhotoImportFoldersAware>>(provider.GetRequiredService<IEnumerable<IPhotoImportFoldersAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<ISyncStateAware>>(provider.GetRequiredService<IEnumerable<ISyncStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<ISyncStatisticsAware>>(provider.GetRequiredService<IEnumerable<ISyncStatisticsAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<ISyncActivityAware>>(provider.GetRequiredService<IEnumerable<ISyncActivityAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IPhotoImportActivityAware>>(provider.GetRequiredService<IEnumerable<IPhotoImportActivityAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IFeatureFlagsAware>>(provider.GetRequiredService<IEnumerable<IFeatureFlagsAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IOfflineStateAware>>(provider.GetRequiredService<IEnumerable<IOfflineStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IIpcMessageHandler>>(provider.GetRequiredService<IEnumerable<IIpcMessageHandler>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IOffersAware>>(provider.GetRequiredService<IEnumerable<IOffersAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IPhotosFeatureStateAware>>(provider.GetRequiredService<IEnumerable<IPhotosFeatureStateAware>>))

                .AddSingleton<ActivityService>()
                .AddSingleton<IAccountStateAware>(provider => provider.GetRequiredService<ActivityService>())
                .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<ActivityService>())

                .AddSingleton<DocumentOpener>()

                .AddSingleton<INotificationClient, NotificationClient>()
                .AddNotificationResources()

                .AddSingleton<OfferService>()
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<OfferService>())
                .AddSingleton<IAccountStateAware>(provider => provider.GetRequiredService<OfferService>())
                .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<OfferService>())
                .AddSingleton<IRemoteSettingsAware>(provider => provider.GetRequiredService<OfferService>())
                .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<OfferService>())

                .AddSingleton<IGoogleTakeoutMetadataExtractor, GoogleTakeoutMetadataExtractor>()
            ;
    }

    public static void InitializeAppServices(this IServiceProvider provider)
    {
        provider.InitializeApiClients();

        provider.GetRequiredService<UserStateChangeHandler>();
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

    private static DriveApiConfig GetDriveApiConfig(IServiceProvider provider)
    {
        var appConfig = provider.GetRequiredService<AppConfig>();
        var config = provider.GetRequiredService<IConfiguration>()
                .GetSection("DriveApi")
                .Get<DriveApiConfig>(options => options.BindNonPublicProperties = true)
            ?? throw new InvalidOperationException($"Cannot instantiate {nameof(DriveApiConfig)} from the configuration");

        config.ClientVersion = config.ClientVersion!
            .Replace("{AppVersion}", appConfig.AppVersion.ToString());
        config.UserAgent = config.UserAgent!
            .Replace("{AppVersion}", appConfig.AppVersion.ToString())
            .Replace("{SystemInfo}", GetSystemInfo());

        return config;
    }

    private static string GetSystemInfo()
    {
        var parts = new List<string>
        {
            $"Windows NT {Environment.OSVersion.Version.ToNormalized()}",
        };

        if (Environment.Is64BitOperatingSystem)
        {
            // System has a 64-bit processor
            parts.Add("Win64");
            parts.Add("x64");

            if (!Environment.Is64BitProcess)
            {
                // A 32-bit version of the app is running on the 64-bit processor
                parts.Add("WOW64");
            }
        }

        return string.Join("; ", parts);
    }
}
