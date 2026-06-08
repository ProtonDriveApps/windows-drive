using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Authentication;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Sync.Shared.Diagnostics.Metrics.Thumbnails;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.FileSystem.Integration;
using ProtonDrive.Sync.Shared.FileSystem.Metadata;
using ProtonDrive.Sync.Shared.FileSystem.Metadata.GoogleTakeout;
using ProtonDrive.Sync.Shared.FileSystem.Metadata.LivePhoto;
using ProtonDrive.Sync.Shared.FileSystem.Metadata.QuickTime;
using ProtonDrive.Sync.Shared.FileSystem.OnDemand;
using ProtonDrive.Sync.Shared.FileSystem.Photos;
using ProtonDrive.Sync.Shared.FileSystem.Thumbnails;
using ProtonDrive.Sync.Windows.FileSystem.Client;
using ProtonDrive.Sync.Windows.FileSystem.Integration;
using ProtonDrive.Sync.Windows.FileSystem.Metadata;
using ProtonDrive.Sync.Windows.FileSystem.OnDemand;
using ProtonDrive.Sync.Windows.FileSystem.Thumbnails;
using ProtonDrive.Sync.Windows.Shell;

namespace ProtonDrive.Sync.Windows.Configuration;

public static class ServiceConfigurator
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddWindowsSyncServices()
        {
            services.AddSingleton<ILocalVolumeInfoProvider, VolumeInfoProvider>();
            services.AddSingleton<ILocalFolderService, LocalFolderService>();
            services.AddSingleton<IFileSystemIdentityProvider<long>, FileSystemIdentityProvider>();
            services.AddSingleton<IReadOnlyFileAttributeRemover, ReadOnlyFileAttributeRemover>();
            services.AddSingleton<IPlaceholderToRegularItemConverter, PlaceholderToRegularItemConverter>();
            services.AddSingleton<INonSyncablePathProvider, NonSyncablePathProvider>();

            services.AddSingleton<ISyncFolderStructureProtector>(provider =>
                    new SafeSyncFolderStructureProtectorDecorator(
                        new LoggingSyncFolderStructureProtectorDecorator(
                            provider.GetRequiredService<ILogger<LoggingSyncFolderStructureProtectorDecorator>>(),
                            new NtfsPermissionsBasedSyncFolderStructureProtector(
                                provider.GetRequiredService<ILogger<NtfsPermissionsBasedSyncFolderStructureProtector>>()))));

            services.AddSingleton<IShellSyncFolderRegistry, Win32ShellSyncFolderRegistry>();
            services.AddSingleton<IFolderAppearanceCustomizer, Win32FolderAppearanceCustomizer>();

            services.AddSingleton<CloudFilterSyncRootRegistry>();
            services.AddSingleton<IOnDemandSyncRootRegistry>(provider => provider.GetRequiredService<CloudFilterSyncRootRegistry>());
            services.AddSingleton<ISessionStateAware>(provider => provider.GetRequiredService<CloudFilterSyncRootRegistry>());

            services.AddSingleton<WinRtFileMetadataExtractor>();
            services.AddSingleton<QuickTimeFileMetadataExtractor>();

            services.AddSingleton<ILivePhotoFileDetector, LivePhotoFileDetector>()
                .AddSingleton<IFileMetadataGenerator>(provider =>
                    new TelemetryFileMetadataGeneratorDecorator(
                        new LivePhotoMetadataExtractingDecorator(
                            new GoogleTakeoutMetadataExtractingDecorator(
                                new QuickTimeFileMetadataExtractingDecorator(
                                    provider.GetRequiredService<WinRtFileMetadataExtractor>(),
                                    provider.GetRequiredService<QuickTimeFileMetadataExtractor>()),
                                provider.GetRequiredService<IGoogleTakeoutMetadataExtractor>()),
                            provider.GetRequiredService<ILivePhotoFileDetector>()),
                        provider.GetRequiredService<IErrorCounter>(),
                        provider.GetRequiredService<ILogger<TelemetryFileMetadataGeneratorDecorator>>()));

            services.AddSingleton<Win32ThumbnailGenerator>();
            services.AddSingleton<SkiaThumbnailGenerator>();

            services.AddSingleton<IThumbnailGenerator>(provider =>
                    new LivePhotoThumbnailExtractingDecorator(
                        provider.GetRequiredService<ILivePhotoFileDetector>(),
                        new DispatchingThumbnailGenerator(
                            [
                                new TelemetryThumbnailGeneratorDecorator(
                                    provider.GetRequiredService<Win32ThumbnailGenerator>(),
                                    ThumbnailGenerationMethod.Win32,
                                    provider.GetRequiredService<IThumbnailGenerationMetricsCollector>(),
                                    provider.GetRequiredService<ILogger<TelemetryThumbnailGeneratorDecorator>>()),
                                new TelemetryThumbnailGeneratorDecorator(
                                    provider.GetRequiredService<SkiaThumbnailGenerator>(),
                                    ThumbnailGenerationMethod.Skia,
                                    provider.GetRequiredService<IThumbnailGenerationMetricsCollector>(),
                                    provider.GetRequiredService<ILogger<TelemetryThumbnailGeneratorDecorator>>()),
                            ],
                            provider.GetRequiredService<ILogger<IThumbnailGenerator>>())));

            services.AddSingleton<IPhotoTagsGenerator, PhotoTagsGenerator>();

            services.AddSingleton<ILocalFileSystemClientFactory, LocalFileSystemClientFactory>();
            services.AddSingleton<ILocalEventLogClientFactory, LocalEventLogClientFactory>();

            return services;
        }
    }
}
