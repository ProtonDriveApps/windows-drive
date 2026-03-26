using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Features;
using ProtonDrive.Shared.Metrics;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Windows.FileSystem.Photos;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

public sealed class LocalFileSystemClientFactory : ILocalFileSystemClientFactory
{
    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly IFileMetadataGenerator _fileMetadataGenerator;
    private readonly IPhotoTagsGenerator _photoTagsGenerator;
    private readonly ILoggerFactory _loggerFactory;

    private readonly Action<MetricEvent> _recordMetric;

    public LocalFileSystemClientFactory(
        IFeatureFlagProvider featureFlagProvider,
        IThumbnailGenerator thumbnailGenerator,
        IFileMetadataGenerator fileMetadataGenerator,
        IPhotoTagsGenerator photoTagsGenerator,
        IMetricsRecorder metricsRecorder,
        ILoggerFactory loggerFactory)
    {
        _featureFlagProvider = featureFlagProvider;
        _thumbnailGenerator = thumbnailGenerator;
        _fileMetadataGenerator = fileMetadataGenerator;
        _photoTagsGenerator = photoTagsGenerator;
        _loggerFactory = loggerFactory;

        _recordMetric = metricsRecorder.Record;
    }

    public IFileSystemClient<long> CreateClassicClient()
    {
        return new ClassicFileSystemClient(
            _featureFlagProvider,
            _thumbnailGenerator,
            _fileMetadataGenerator,
            _photoTagsGenerator,
            _recordMetric);
    }

    public IFileSystemClient<long> CreateOnDemandHydrationClient()
    {
        return new OnDemandHydrationFileSystemClient(
            _featureFlagProvider,
            _thumbnailGenerator,
            _fileMetadataGenerator,
            _photoTagsGenerator,
            _recordMetric,
            _loggerFactory);
    }

    public IPhotoFileSystemClient<long> CreatePhotoClient()
    {
        return new PhotoFileSystemClient(CreateClassicClient());
    }
}
