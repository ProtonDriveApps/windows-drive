using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Account;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Configuration;
using ProtonDrive.App.Devices;
using ProtonDrive.App.EarlyAccess;
using ProtonDrive.App.FileSystem.Metadata.GoogleTakeout;
using ProtonDrive.App.InterProcessCommunication;
using ProtonDrive.App.Localization;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.Notifications;
using ProtonDrive.App.Notifications.Offers;
using ProtonDrive.App.Onboarding;
using ProtonDrive.App.Photos;
using ProtonDrive.App.Photos.Import;
using ProtonDrive.App.Photos.LivePhoto;
using ProtonDrive.App.Services;
using ProtonDrive.App.Settings;
using ProtonDrive.App.Sync;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.App.Volumes;
using ProtonDrive.App.Windows.Authentication;
using ProtonDrive.App.Windows.Configuration.Hyperlinks;
using ProtonDrive.App.Windows.Dialogs;
using ProtonDrive.App.Windows.Dialogs.HumanVerification;
using ProtonDrive.App.Windows.InterProcessCommunication;
using ProtonDrive.App.Windows.Services;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.App.Windows.Toolkit.Threading;
using ProtonDrive.App.Windows.Views.BugReport;
using ProtonDrive.App.Windows.Views.Main;
using ProtonDrive.App.Windows.Views.Main.About;
using ProtonDrive.App.Windows.Views.Main.Account;
using ProtonDrive.App.Windows.Views.Main.Activity;
using ProtonDrive.App.Windows.Views.Main.MyComputer;
using ProtonDrive.App.Windows.Views.Main.Photos;
using ProtonDrive.App.Windows.Views.Main.Settings;
using ProtonDrive.App.Windows.Views.Main.SharedWithMe;
using ProtonDrive.App.Windows.Views.Offer;
using ProtonDrive.App.Windows.Views.Onboarding;
using ProtonDrive.App.Windows.Views.Shared;
using ProtonDrive.App.Windows.Views.Shared.Navigation;
using ProtonDrive.App.Windows.Views.SignIn;
using ProtonDrive.App.Windows.Views.SystemTray;
using ProtonDrive.Shared.Features;
using ProtonDrive.Shared.HumanVerification;
using ProtonDrive.Shared.Localization;
using ProtonDrive.Shared.Offline;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Security.Cryptography;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.FileSystem.Photos;
using ProtonDrive.Sync.Windows.FileSystem.Client;
using ProtonDrive.Sync.Windows.FileSystem.Photos;
using ProtonDrive.Sync.Windows.Security.Cryptography;

namespace ProtonDrive.App.Windows.Configuration;

internal static class AppServices
{
    public static IHostBuilder AddApp(this IHostBuilder builder, AppArguments appArguments)
    {
        return builder.ConfigureServices(
            services =>
                services
                    .AddSingleton(appArguments)
                    .AddSingleton<AppLifecycleLogger>()
                    .AddSingleton<AppLifecycleService>()
                    .AddSingleton<App>()
                    .AddSingleton<IApp>(provider => provider.GetRequiredService<App>())
                    .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<App>())
                    .AddSingleton<IOnboardingStateAware>(provider => provider.GetRequiredService<App>()));
    }

    public static IHostBuilder AddServices(this IHostBuilder builder)
    {
        return builder.ConfigureServices(AddWindowsAppServices);
    }

    public static void InitializeServices(this IServiceProvider provider)
    {
        provider.InitializeAppServices();
    }

    private static void AddWindowsAppServices(IServiceCollection services)
    {
        services
            .AddAppServices()

            .AddSingleton(new DispatcherScheduler(Dispatcher.CurrentDispatcher))
            .AddSingleton<IHumanVerifier, HumanVerifier>()
            .AddKeyedSingleton<IScheduler>("Dispatcher", (sp, _) => sp.GetRequiredService<DispatcherScheduler>())

            .AddSingleton<IFileSystemDisplayNameAndIconProvider, Win32FileSystemDisplayNameAndIconProvider>()
            .AddSingleton<IFileSystemItemTypeProvider>(_ => new CachingFileSystemItemTypeProvider(new Win32FileSystemItemTypeProvider()))
            .AddSingleton<IOperatingSystemIntegrationService, OperatingSystemIntegrationService>()
            .AddSingleton<ILocalVolumeInfoProvider, VolumeInfoProvider>()
            .AddSingleton<ILocalFolderService, LocalFolderService>()
            .AddSingleton<IKnownFolders, KnownFolders>()
            .AddSingleton<IReadOnlyFileAttributeRemover, ReadOnlyFileAttributeRemover>()
            .AddSingleton<IPlaceholderToRegularItemConverter, PlaceholderToRegularItemConverter>()
            .AddSingleton<INonSyncablePathProvider, NonSyncablePathProvider>()
            .AddSingleton<INotificationService, SystemToastNotificationService>()
            .AddSingleton<IDialogService, DialogService>()
            .AddSingleton<IUrlOpener, UrlOpener>()
            .AddSingleton<IForkingSessionUrlOpener, ForkingSessionUrlOpener>()
            .AddSingleton<IExternalHyperlinks, ExternalHyperlinks>()
            .AddSingleton<IClipboard, SystemClipboard>()
            .AddSingleton<IDataProtectionProvider, DataProtectionProvider>()
            .AddSingleton<ISyncFolderStructureProtector>(
                provider =>
                    new SafeSyncFolderStructureProtectorDecorator(
                        new LoggingSyncFolderStructureProtectorDecorator(
                            provider.GetRequiredService<ILogger<LoggingSyncFolderStructureProtectorDecorator>>(),
                            new NtfsPermissionsBasedSyncFolderStructureProtector(
                                provider.GetRequiredService<ILogger<NtfsPermissionsBasedSyncFolderStructureProtector>>()))))
            .AddSingleton<IShellSyncFolderRegistry, Win32ShellSyncFolderRegistry>()
            .AddSingleton<WinRegistryLanguageRepository>()
            .AddSingleton<IRepository<LanguageSettings>, WinRegistryLanguageRepository>()

            .AddSingleton<LanguageService>()
            .AddSingleton<ILanguageService>(provider => provider.GetRequiredService<LanguageService>())
            .AddSingleton<ILanguageProvider>(provider => provider.GetRequiredService<LanguageService>())

            .AddSingleton<CloudFilterSyncRootRegistry>()
            .AddSingleton<IOnDemandSyncRootRegistry>(provider => provider.GetRequiredService<CloudFilterSyncRootRegistry>())
            .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<CloudFilterSyncRootRegistry>())

            .AddSingleton<IFolderAppearanceCustomizer, Win32FolderAppearanceCustomizer>()

            .AddSingleton<UpdateNotificationService>()
            .AddSingleton<IStartableService>(provider => provider.GetRequiredService<UpdateNotificationService>())

            .AddSingleton<OfferNotificationService>()
            .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<OfferNotificationService>())
            .AddSingleton<IOnboardingStateAware>(provider => provider.GetRequiredService<OfferNotificationService>())
            .AddSingleton<IOffersAware>(provider => provider.GetRequiredService<OfferNotificationService>())
            .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<OfferNotificationService>())

            .AddSingleton<AppCommands>()
            .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<AppCommands>())
            .AddSingleton<ISyncFoldersAware>(provider => provider.GetRequiredService<AppCommands>())
            .AddSingleton<AppStateViewModel>()
            .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<AppStateViewModel>())
            .AddSingleton<IMainVolumeStateAware>(provider => provider.GetRequiredService<AppStateViewModel>())
            .AddSingleton<IAccountStateAware>(provider => provider.GetRequiredService<AppStateViewModel>())
            .AddSingleton<IMappingsSetupStateAware>(provider => provider.GetRequiredService<AppStateViewModel>())
            .AddSingleton<ISyncStateAware>(provider => provider.GetRequiredService<AppStateViewModel>())
            .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<AppStateViewModel>())
            .AddSingleton<IOfflineStateAware>(provider => provider.GetRequiredService<AppStateViewModel>())
            .AddSingleton<SessionWorkflowViewModel>()
            .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<SessionWorkflowViewModel>())
            .AddSingleton<NavigationService<DetailsPageViewModel>>()
            .AddSingleton<INavigationService<DetailsPageViewModel>>(provider => provider.GetRequiredService<NavigationService<DetailsPageViewModel>>())
            .AddSingleton<INavigatablePages<DetailsPageViewModel>>(provider => provider.GetRequiredService<NavigationService<DetailsPageViewModel>>())
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<PageViewModelFactory>()
            .AddSingleton<IUpgradeStoragePlanAvailabilityVerifier, UpgradeStoragePlanAvailabilityVerifier>()

            .AddSingleton<OnboardingViewModel>()
            .AddSingleton<IOnboardingStateAware>(provider => provider.GetRequiredService<OnboardingViewModel>())
            .AddSingleton<SyncFolderSelectionStepViewModel>()
            .AddSingleton<AccountRootFolderSelectionStepViewModel>()
            .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<AccountRootFolderSelectionStepViewModel>())
            .AddSingleton<UpgradeStorageStepViewModel>()
            .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<UpgradeStorageStepViewModel>())

            .AddSingleton<NotificationBadgeProvider>()
            .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<NotificationBadgeProvider>())
            .AddSingleton<ISyncFoldersAware>(provider => provider.GetRequiredService<NotificationBadgeProvider>())
            .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<NotificationBadgeProvider>())
            .AddSingleton<IStorageOptimizationOnboardingStateAware>(provider => provider.GetRequiredService<NotificationBadgeProvider>())
            .AddSingleton<IPhotosFeatureStateAware>(provider => provider.GetRequiredService<NotificationBadgeProvider>())

            .AddSingleton<MainViewModel>()
            .AddSingleton<IApplicationPages>(provider => provider.GetRequiredService<MainViewModel>())
            .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<MainViewModel>())
            .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<MainViewModel>())
            .AddSingleton<IAccountStateAware>(provider => provider.GetRequiredService<MainViewModel>())
            .AddSingleton<IOffersAware>(provider => provider.GetRequiredService<MainViewModel>())
            .AddSingleton<IPhotosFeatureStateAware>(provider => provider.GetRequiredService<MainViewModel>())

            .AddTransient<AddFoldersValidationResultMessageBuilder>()
            .AddTransient<AddFoldersViewModel>()
            .AddTransient<Func<AddFoldersViewModel>>(provider => provider.GetRequiredService<AddFoldersViewModel>)

            .AddSingleton<MyComputerViewModel>()
            .AddSingleton<IDeviceServiceStateAware>(provider => provider.GetRequiredService<MyComputerViewModel>())
            .AddSingleton<IDevicesAware>(provider => provider.GetRequiredService<MyComputerViewModel>())
            .AddSingleton<IMappingStateAware>(provider => provider.GetRequiredService<MyComputerViewModel>())
            .AddSingleton<IStorageOptimizationOnboardingStateAware>(provider => provider.GetRequiredService<MyComputerViewModel>())
            .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<MyComputerViewModel>())

            .AddSingleton<FolderListViewModel>()
            .AddSingleton<ISyncFoldersAware>(provider => provider.GetRequiredService<FolderListViewModel>())
            .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<FolderListViewModel>())

            .AddTransient<RemoveClassicFolderConfirmationViewModel>()
            .AddSingleton<Func<RemoveClassicFolderConfirmationViewModel>>(provider => provider.GetRequiredService<RemoveClassicFolderConfirmationViewModel>)

            .AddTransient<RemoveOnDemandFolderConfirmationViewModel>()
            .AddSingleton<Func<RemoveOnDemandFolderConfirmationViewModel>>(provider => provider.GetRequiredService<RemoveOnDemandFolderConfirmationViewModel>)

            .AddTransient<StorageOptimizationTurnedOffNotificationViewModel>()
            .AddSingleton<Func<StorageOptimizationTurnedOffNotificationViewModel>>(provider => provider.GetRequiredService<StorageOptimizationTurnedOffNotificationViewModel>)

            .AddTransient<StorageOptimizationUnavailableNotificationViewModel>()
            .AddSingleton<Func<StorageOptimizationUnavailableNotificationViewModel>>(provider => provider.GetRequiredService<StorageOptimizationUnavailableNotificationViewModel>)

            .AddSingleton<SharedWithMeViewModel>()
            .AddSingleton<ISharedWithMeOnboardingStateAware>(provider => provider.GetRequiredService<SharedWithMeViewModel>())

            .AddSingleton<SharedWithMeListViewModel>()
            .AddSingleton<ISyncFoldersAware>(provider => provider.GetRequiredService<SharedWithMeListViewModel>())
            .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<SharedWithMeListViewModel>())
            .AddSingleton<ISyncStateAware>(provider => provider.GetRequiredService<SharedWithMeListViewModel>())
            .AddSingleton<SharedWithMeItemViewModelFactory>()

            .AddSingleton<PhotosViewModel>()
            .AddSingleton<PhotosImportViewModel>()
            .AddSingleton<ISyncFoldersAware>(provider => provider.GetRequiredService<PhotosImportViewModel>())
            .AddSingleton<IPhotoImportFoldersAware>(provider => provider.GetRequiredService<PhotosImportViewModel>())
            .AddSingleton<IAccountSwitchingAware>(provider => provider.GetRequiredService<PhotosImportViewModel>())
            .AddSingleton<IPhotosFeatureStateAware>(provider => provider.GetRequiredService<PhotosImportViewModel>())

            .AddSingleton<SettingsViewModel>()
            .AddSingleton<IEarlyAccessStateAware>(provider => provider.GetRequiredService<SettingsViewModel>())
            .AddSingleton<AboutViewModel>()
            .AddTransient<BugReportViewModel>()
            .AddSingleton<Func<BugReportViewModel>>(provider => provider.GetRequiredService<BugReportViewModel>)
            .AddSingleton<AccountViewModel>()
            .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<AccountViewModel>())
            .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<AccountViewModel>())
            .AddSingleton<AccountRootSyncFolderViewModel>()
            .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<AccountRootSyncFolderViewModel>())
            .AddSingleton<ISyncFoldersAware>(provider => provider.GetRequiredService<AccountRootSyncFolderViewModel>())
            .AddSingleton<RenameRemoteNodeViewModel>()
            .AddSingleton<ISyncStateAware>(provider => provider.GetRequiredService<RenameRemoteNodeViewModel>())
            .AddSingleton<SyncStateViewModel>()
            .AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<SyncStateViewModel>())
            .AddSingleton<ISyncStateAware>(provider => provider.GetRequiredService<SyncStateViewModel>())
            .AddSingleton<ISyncActivityAware>(provider => provider.GetRequiredService<SyncStateViewModel>())
            .AddSingleton<ISyncStatisticsAware>(provider => provider.GetRequiredService<SyncStateViewModel>())
            .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<SyncStateViewModel>())
            .AddSingleton<SystemTrayViewModel>()

            .AddTransient<OfferViewModel>()
            .AddSingleton<Func<OfferViewModel>>(provider => provider.GetRequiredService<OfferViewModel>)

            .AddSingleton(
                provider => new NamedPipeBasedIpcServer(
                    NamedPipeBasedIpcServer.PipeName,
                    provider.GetRequiredService<Lazy<IEnumerable<IIpcMessageHandler>>>(),
                    provider.GetRequiredService<ILogger<NamedPipeBasedIpcServer>>()))
            .AddSingleton<IStartableService>(provider => provider.GetRequiredService<NamedPipeBasedIpcServer>())
            .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<NamedPipeBasedIpcServer>())

            .AddSingleton<ILivePhotoFileDetector, LivePhotoFileDetector>()
            .AddSingleton<WinRtFileMetadataExtractor>()
            .AddSingleton<QuickTimeFileMetadataExtractor>()
            .AddSingleton<IFileMetadataGenerator>(
                provider =>
                    new LivePhotoMetadataExtractingDecorator(
                        new GoogleTakeoutMetadataExtractingDecorator(
                            new QuickTimeFileMetadataExtractingDecorator(
                                provider.GetRequiredService<WinRtFileMetadataExtractor>(),
                                provider.GetRequiredService<QuickTimeFileMetadataExtractor>()),
                            provider.GetRequiredService<IGoogleTakeoutMetadataExtractor>()),
                        provider.GetRequiredService<ILivePhotoFileDetector>()))

            .AddSingleton<Win32ThumbnailGenerator>()
            .AddSingleton<SkiaThumbnailGenerator>()
            .AddSingleton<IThumbnailGenerator>(
                provider =>
                    new LivePhotoThumbnailExtractingDecorator(
                        provider.GetRequiredService<ILivePhotoFileDetector>(),
                        new DispatchingThumbnailGenerator(
                        [
                            provider.GetRequiredService<Win32ThumbnailGenerator>(),
                            provider.GetRequiredService<SkiaThumbnailGenerator>(),
                        ],
                        provider.GetRequiredService<ILogger<IThumbnailGenerator>>())))
            .AddSingleton<IPhotoTagsGenerator, PhotoTagsGenerator>()

            .AddSingleton<ILocalFileSystemClientFactory, LocalFileSystemClientFactory>()
            .AddSingleton<ILocalEventLogClientFactory, LocalEventLogClientFactory>()

            .AddSingleton<IFileSystemIdentityProvider<long>, FileSystemIdentityProvider>()

            .AddSingleton<IFido2Authenticator, Win32Fido2Authenticator>()
            ;
    }
}
