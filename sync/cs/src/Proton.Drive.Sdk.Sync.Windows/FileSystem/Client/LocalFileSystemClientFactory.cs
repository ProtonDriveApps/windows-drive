using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.Metrics;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Client;

public sealed class LocalFileSystemClientFactory : ILocalFileSystemClientFactory
{
    private readonly ILocalVolumeInfoProvider _localVolumeInfoProvider;
    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly IFileMetadataGenerator _fileMetadataGenerator;
    private readonly IPhotoTagsGenerator _photoTagsGenerator;
    private readonly ILoggerFactory _loggerFactory;
    private readonly EnumerationOptions? _enumerationOptions;

    private readonly Action<MetricEvent> _recordMetric;

    public LocalFileSystemClientFactory(
        ILocalVolumeInfoProvider localVolumeInfoProvider,
        IFeatureFlagProvider featureFlagProvider,
        IThumbnailGenerator thumbnailGenerator,
        IFileMetadataGenerator fileMetadataGenerator,
        IPhotoTagsGenerator photoTagsGenerator,
        IMetricsRecorder metricsRecorder,
        ILoggerFactory loggerFactory,
        EnumerationOptions? enumerationOptions = null)
    {
        _localVolumeInfoProvider = localVolumeInfoProvider;
        _featureFlagProvider = featureFlagProvider;
        _thumbnailGenerator = thumbnailGenerator;
        _fileMetadataGenerator = fileMetadataGenerator;
        _photoTagsGenerator = photoTagsGenerator;
        _loggerFactory = loggerFactory;
        _enumerationOptions = enumerationOptions;

        _recordMetric = metricsRecorder.Record;
    }

    public IFileSystemClient<long> CreateClassicClient()
    {
        return new ClassicFileSystemClient(
            _featureFlagProvider,
            _thumbnailGenerator,
            _fileMetadataGenerator,
            _photoTagsGenerator,
            _recordMetric,
            _loggerFactory);
    }

    public IFileSystemClient<long> CreateOnDemandHydrationClient()
    {
        return new OnDemandHydrationFileSystemClient(
            _featureFlagProvider,
            _thumbnailGenerator,
            _fileMetadataGenerator,
            _photoTagsGenerator,
            _localVolumeInfoProvider,
            _recordMetric,
            _loggerFactory,
            _enumerationOptions);
    }

    public IPhotoFileSystemClient<long> CreatePhotoClient()
    {
        return new PhotoFileSystemClient(CreateClassicClient());
    }
}
