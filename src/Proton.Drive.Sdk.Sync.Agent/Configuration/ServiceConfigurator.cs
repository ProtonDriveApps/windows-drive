using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Agent.Authentication;
using Proton.Drive.Sdk.Sync.Agent.Devices;
using Proton.Drive.Sdk.Sync.Agent.Diagnostics.Observability;
using Proton.Drive.Sdk.Sync.Agent.Diagnostics.Observability.TransferPerformance;
using Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry;
using Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.Errors;
using Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.FileIntegrity;
using Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.MappingSetup;
using Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.Synchronization;
using Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.ThumbnailGeneration;
using Proton.Drive.Sdk.Sync.Agent.Drive.Services;
using Proton.Drive.Sdk.Sync.Agent.Drive.Services.Events;
using Proton.Drive.Sdk.Sync.Agent.Features;
using Proton.Drive.Sdk.Sync.Agent.FileSystem.Local;
using Proton.Drive.Sdk.Sync.Agent.FileSystem.Remote;
using Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.CloudFiles;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.ForeignDevices;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.HostDeviceFolders;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.SharedWithMe.SharedWithMeItem;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.SharedWithMe.SharedWithMeRootFolder;
using Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;
using Proton.Drive.Sdk.Sync.Agent.Mapping.Teardown;
using Proton.Drive.Sdk.Sync.Agent.Reporting;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Agent.Settings.Remote;
using Proton.Drive.Sdk.Sync.Agent.Volumes;
using Proton.Drive.Sdk.Sync.Client.Configuration;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Sdk.Sync.Shared.Diagnostics.Metrics.Thumbnails;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata.GoogleTakeout;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Caching;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Devices;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.HumanVerification;
using Proton.Drive.Shared.Net.Http;
using Proton.Drive.Shared.Net.Http.TlsPinning;
using Proton.Drive.Shared.Offline;
using Proton.Drive.Shared.Reporting;
using Proton.Drive.Shared.Repository;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Configuration;

public static class ServiceConfigurator
{
    public static IServiceCollection AddSyncAgentServices(this IServiceCollection services)
    {
        return services
                .AddSingleton<IErrorReporting, ErrorReporting>()
                .AddSingleton<SentryOptionsProvider>()

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

                .ReplaceHttpClientLogging()

                .AddSingleton<IClock, SystemClock>()

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
                        new ClearingOnAccountSwitchingRepositoryDecorator<DeviceSettings>(
                            provider.GetRequiredService<IRepositoryFactory>()
                                .GetCachingRepository<DeviceSettings>("DeviceSettings.json")))
                .AddSingleton<IRepository<DeviceSettings>>(
                    provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<DeviceSettings>>())
                .AddSingleton<IAccountSwitchingHandler>(
                    provider => provider.GetRequiredService<ClearingOnAccountSwitchingRepositoryDecorator<DeviceSettings>>())

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

                .AddSingleton<DeviceService>()
                .AddSingleton<IDeviceService>(provider => provider.GetRequiredService<DeviceService>())
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<DeviceService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<DeviceService>())
                .AddSingleton<IMainVolumeStateAware>(provider => provider.GetRequiredService<DeviceService>())
                .AddSingleton<IRemoteDeviceEventsAware>(provider => provider.GetRequiredService<DeviceService>())
                .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<DeviceService>())

                .AddSingleton<SyncFolderService>()
                .AddSingleton<ISyncFolderService>(provider => provider.GetRequiredService<SyncFolderService>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<SyncFolderService>())

                .AddSingleton<SyncFolderProvider>()
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<SyncFolderProvider>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<SyncFolderProvider>())
                .AddSingleton<IMappingStateAware>(provider => provider.GetRequiredService<SyncFolderProvider>())

                .AddSingleton<MappingRegistry>()
                .AddSingleton<IMappingRegistry>(provider => provider.GetRequiredService<MappingRegistry>())
                .AddSingleton<IStartableService>(provider => provider.GetRequiredService<MappingRegistry>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<MappingRegistry>())

                .AddSingleton<MappingSetupService>()
                .AddSingleton<IMappingSetupService>(provider => provider.GetRequiredService<MappingSetupService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<MappingSetupService>())
                .AddSingleton<IMappingsAware>(provider => provider.GetRequiredService<MappingSetupService>())
                .AddSingleton<ISyncLifecycleStateAware>(provider => provider.GetRequiredService<MappingSetupService>())
                .AddSingleton<ISyncStateAware>(provider => provider.GetRequiredService<MappingSetupService>())
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

                .AddSingleton<SyncLifecycleService>()
                .AddSingleton<ISyncLifecycleService>(provider => provider.GetRequiredService<SyncLifecycleService>())
                .AddSingleton<IMainVolumeStateAware>(provider => provider.GetRequiredService<SyncLifecycleService>())

                .AddSingleton<SyncService>()
                .AddSingleton<ISyncService>(provider => provider.GetRequiredService<SyncService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<SyncService>())
                .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<SyncService>())
                .AddSingleton<IMappingsSetupStateAware>(provider => provider.GetRequiredService<SyncService>())
                .AddSingleton<IOfflineStateAware>(provider => provider.GetRequiredService<SyncService>())
                .AddSingleton<IMappedFileSystemIdentityProvider>(provider => provider.GetRequiredService<SyncService>())
                .AddSingleton<ISyncRootPathProvider>(provider => provider.GetRequiredService<SyncService>())

                .AddSingleton<SyncResumptionHandler>()
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<SyncResumptionHandler>())
                .AddSingleton<ISyncStateAware>(provider => provider.GetRequiredService<SyncResumptionHandler>())

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

                .AddSingleton<CoreEventService>()
                .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<CoreEventService>())
                .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<CoreEventService>())

                .AddSingleton<UserStateChangeHandler>()

                .AddSingleton<TelemetryService>()
                .AddSingleton<IRemoteSettingsAware>(provider => provider.GetRequiredService<TelemetryService>())

                .AddSingleton<IPeriodicTelemetryReportProvider, ErrorReportProvider>()
                .AddSingleton<IPeriodicTelemetryReportProvider, FileIntegrityReportProvider>()
                .AddSingleton<IPeriodicTelemetryReportProvider, MappingSetupReportProvider>()

                .AddSingleton<SynchronizationReportProvider>()
                .AddSingleton<IPeriodicTelemetryReportProvider>(provider => provider.GetRequiredService<SynchronizationReportProvider>())
                .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<SynchronizationReportProvider>())

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
                .AddSingleton(provider => new Lazy<IEnumerable<IAccountStateAware>>(provider.GetRequiredService<IEnumerable<IAccountStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IAccountSwitchingAware>>(provider.GetRequiredService<IEnumerable<IAccountSwitchingAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IUserStateAware>>(provider.GetRequiredService<IEnumerable<IUserStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IMainVolumeStateAware>>(provider.GetRequiredService<IEnumerable<IMainVolumeStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IDeviceServiceStateAware>>(provider.GetRequiredService<IEnumerable<IDeviceServiceStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IDevicesAware>>(provider.GetRequiredService<IEnumerable<IDevicesAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IMappingsAware>>(provider.GetRequiredService<IEnumerable<IMappingsAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IMappingStateAware>>(provider.GetRequiredService<IEnumerable<IMappingStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IMappingsSetupStateAware>>(provider.GetRequiredService<IEnumerable<IMappingsSetupStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IAccountSwitchingHandler>>(provider.GetRequiredService<IEnumerable<IAccountSwitchingHandler>>))
                .AddSingleton(provider => new Lazy<IEnumerable<ISyncFoldersAware>>(provider.GetRequiredService<IEnumerable<ISyncFoldersAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<ISyncLifecycleStateAware>>(provider.GetRequiredService<IEnumerable<ISyncLifecycleStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<ISyncStateAware>>(provider.GetRequiredService<IEnumerable<ISyncStateAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<ISyncStatisticsAware>>(provider.GetRequiredService<IEnumerable<ISyncStatisticsAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<ISyncActivityAware>>(provider.GetRequiredService<IEnumerable<ISyncActivityAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IFeatureFlagsAware>>(provider.GetRequiredService<IEnumerable<IFeatureFlagsAware>>))
                .AddSingleton(provider => new Lazy<IEnumerable<IOfflineStateAware>>(provider.GetRequiredService<IEnumerable<IOfflineStateAware>>))

                .AddSingleton<IGoogleTakeoutMetadataExtractor, GoogleTakeoutMetadataExtractor>()
            ;
    }

    public static void InitializeSyncAgentServices(this IServiceProvider provider)
    {
        provider.InitializeApiClients();

        provider.GetRequiredService<UserStateChangeHandler>();
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
