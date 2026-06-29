using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proton.Drive.App.Configuration;
using Proton.Drive.App.EarlyAccess;
using Proton.Drive.App.InterProcessCommunication;
using Proton.Drive.App.Localization;
using Proton.Drive.App.Notifications;
using Proton.Drive.App.Notifications.Offers;
using Proton.Drive.App.Onboarding;
using Proton.Drive.App.Photos;
using Proton.Drive.App.Photos.Import;
using Proton.Drive.App.Settings;
using Proton.Drive.App.SystemIntegration;
using Proton.Drive.App.Windows.Authentication;
using Proton.Drive.App.Windows.Configuration.Hyperlinks;
using Proton.Drive.App.Windows.Diagnostics.Telemetry.FirstLaunch;
using Proton.Drive.App.Windows.Dialogs;
using Proton.Drive.App.Windows.Dialogs.HumanVerification;
using Proton.Drive.App.Windows.InterProcessCommunication;
using Proton.Drive.App.Windows.Services;
using Proton.Drive.App.Windows.SystemIntegration;
using Proton.Drive.App.Windows.Toolkit.Threading;
using Proton.Drive.App.Windows.Views.BugReport;
using Proton.Drive.App.Windows.Views.Main;
using Proton.Drive.App.Windows.Views.Main.About;
using Proton.Drive.App.Windows.Views.Main.Account;
using Proton.Drive.App.Windows.Views.Main.Activity;
using Proton.Drive.App.Windows.Views.Main.MyComputer;
using Proton.Drive.App.Windows.Views.Main.Photos;
using Proton.Drive.App.Windows.Views.Main.Settings;
using Proton.Drive.App.Windows.Views.Main.SharedWithMe;
using Proton.Drive.App.Windows.Views.Offer;
using Proton.Drive.App.Windows.Views.Onboarding;
using Proton.Drive.App.Windows.Views.Shared;
using Proton.Drive.App.Windows.Views.Shared.Navigation;
using Proton.Drive.App.Windows.Views.SignIn;
using Proton.Drive.App.Windows.Views.SystemTray;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Agent.Configuration;
using Proton.Drive.Sdk.Sync.Agent.Devices;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Sdk.Sync.Agent.Volumes;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Sdk.Sync.Windows.Configuration;
using Proton.Drive.Sdk.Sync.Windows.Security.Cryptography;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.HumanVerification;
using Proton.Drive.Shared.Localization;
using Proton.Drive.Shared.Offline;
using Proton.Drive.Shared.Repository;
using Proton.Drive.Shared.Security.Cryptography;
using Proton.Drive.Shared.Telemetry;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Windows.Configuration;

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
        provider.InitializeSyncAgentServices();
    }

    private static void AddWindowsAppServices(IServiceCollection services)
    {
        services
            .AddAppServices()
            .AddWindowsSyncServices()

            .AddSingleton(new DispatcherScheduler(Dispatcher.CurrentDispatcher))
            .AddKeyedSingleton<IScheduler>("Dispatcher", (sp, _) => sp.GetRequiredService<DispatcherScheduler>())

            .AddSingleton<IHumanVerifier, HumanVerifier>()
            .AddSingleton<IFileSystemDisplayNameAndIconProvider, Win32FileSystemDisplayNameAndIconProvider>()
            .AddSingleton<IOperatingSystemIntegrationService, OperatingSystemIntegrationService>()
            .AddSingleton<IKnownFolders, KnownFolders>()
            .AddSingleton<INotificationService, SystemToastNotificationService>()
            .AddSingleton<IDialogService, DialogService>()
            .AddSingleton<IUrlOpener, UrlOpener>()
            .AddSingleton<IForkingSessionUrlOpener, ForkingSessionUrlOpener>()
            .AddSingleton<IExternalHyperlinks, ExternalHyperlinks>()
            .AddSingleton<IClipboard, SystemClipboard>()
            .AddSingleton<IDataProtectionProvider, DataProtectionProvider>()

            .AddSingleton<WinRegistryLanguageRepository>()
            .AddSingleton<IRepository<LanguageSettings>, WinRegistryLanguageRepository>()

            .AddSingleton<LanguageService>()
            .AddSingleton<ILanguageService>(provider => provider.GetRequiredService<LanguageService>())
            .AddSingleton<ILanguageProvider>(provider => provider.GetRequiredService<LanguageService>())

            .AddSingleton<UpdateNotificationService>()
            .AddSingleton<IStartableService>(provider => provider.GetRequiredService<UpdateNotificationService>())

            .AddSingleton<OfferNotificationService>()
            .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<OfferNotificationService>())
            .AddSingleton<IOnboardingStateAware>(provider => provider.GetRequiredService<OfferNotificationService>())
            .AddSingleton<IOffersAware>(provider => provider.GetRequiredService<OfferNotificationService>())
            .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<OfferNotificationService>())

            .AddSingleton<UpgradeStorageNotificationService>()
            .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<UpgradeStorageNotificationService>())
            .AddSingleton<IOffersAware>(provider => provider.GetRequiredService<UpgradeStorageNotificationService>())
            .AddSingleton<IAccountSwitchingAware>(provider => provider.GetRequiredService<UpgradeStorageNotificationService>())

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
            .AddSingleton<IOffersAware>(provider => provider.GetRequiredService<UpgradeStorageStepViewModel>())

            .AddSingleton<NotificationBadgeProvider>()
            .AddSingleton<IUserStateAware>(provider => provider.GetRequiredService<NotificationBadgeProvider>())
            .AddSingleton<ISyncFoldersAware>(provider => provider.GetRequiredService<NotificationBadgeProvider>())
            .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<NotificationBadgeProvider>())

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
            .AddSingleton<Func<StorageOptimizationTurnedOffNotificationViewModel>>(provider =>
                provider.GetRequiredService<StorageOptimizationTurnedOffNotificationViewModel>)

            .AddTransient<StorageOptimizationUnavailableNotificationViewModel>()
            .AddSingleton<Func<StorageOptimizationUnavailableNotificationViewModel>>(provider =>
                provider.GetRequiredService<StorageOptimizationUnavailableNotificationViewModel>)

            .AddSingleton<SharedWithMeViewModel>()
            .AddSingleton<ISharedWithMeOnboardingStateAware>(provider => provider.GetRequiredService<SharedWithMeViewModel>())

            .AddSingleton<SharedWithMeListViewModel>()
            .AddSingleton<ISyncFoldersAware>(provider => provider.GetRequiredService<SharedWithMeListViewModel>())
            .AddSingleton<IFeatureFlagsAware>(provider => provider.GetRequiredService<SharedWithMeListViewModel>())
            .AddSingleton<ISyncStateAware>(provider => provider.GetRequiredService<SharedWithMeListViewModel>())
            .AddSingleton<SharedWithMeItemViewModelFactory>()

            .AddSingleton<PhotosViewModel>()
            .AddSingleton<PhotosImportViewModel>()
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

            .AddSingleton<SystemTrayViewModel>()

            .AddTransient<OfferViewModel>()
            .AddSingleton<Func<OfferViewModel>>(provider => provider.GetRequiredService<OfferViewModel>)

            .AddSingleton<IOneTimeTelemetryReportProvider, FirstLaunchReportProvider>()

            .AddSingleton(provider => new NamedPipeBasedIpcServer(
                NamedPipeBasedIpcServer.PipeName,
                provider.GetRequiredService<Lazy<IEnumerable<IIpcMessageHandler>>>(),
                provider.GetRequiredService<ILogger<NamedPipeBasedIpcServer>>()))
            .AddSingleton<IStartableService>(provider => provider.GetRequiredService<NamedPipeBasedIpcServer>())
            .AddSingleton<IStoppableService>(provider => provider.GetRequiredService<NamedPipeBasedIpcServer>())

            .AddSingleton<IFido2Authenticator, Win32Fido2Authenticator>()
            ;
    }
}
