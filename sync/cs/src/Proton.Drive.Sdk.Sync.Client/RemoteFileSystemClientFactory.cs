using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Client.Configuration;
using Proton.Drive.Sdk.Sync.Client.Cryptography;
using Proton.Drive.Sdk.Sync.Client.MediaTypes;
using Proton.Drive.Sdk.Sync.Client.RemoteNodes;
using Proton.Drive.Sdk.Sync.Client.Sdk;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.Metrics;

namespace Proton.Drive.Sdk.Sync.Client;

internal sealed class RemoteFileSystemClientFactory : IRemoteFileSystemClientFactory
{
    private readonly DriveApiConfig _driveApiConfig;
    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly IFileContentTypeProvider _fileContentTypeProvider;
    private readonly IRemoteNodeService _remoteNodeService;
    private readonly ILinkApiClient _linkApiClient;
    private readonly IFolderApiClient _folderApiClient;
    private readonly IFileApiClient _fileApiClient;
    private readonly IPhotoApiClient _photoApiClient;
    private readonly ISdkClientFactory _sdkClientFactory;
    private readonly ICryptographyService _cryptographyService;
    private readonly ILoggerFactory _loggerFactory;

    private readonly Action<MetricEvent> _recordMetric;

    public RemoteFileSystemClientFactory(
        DriveApiConfig driveApiConfig,
        IFeatureFlagProvider featureFlagProvider,
        IFileContentTypeProvider fileContentTypeProvider,
        IRemoteNodeService remoteNodeService,
        ILinkApiClient linkApiClient,
        IFolderApiClient folderApiClient,
        IFileApiClient fileApiClient,
        IPhotoApiClient photoApiClient,
        ISdkClientFactory sdkClientFactory,
        ICryptographyService cryptographyService,
        IMetricsRecorder metricsRecorder,
        ILoggerFactory loggerFactory)
    {
        _driveApiConfig = driveApiConfig;
        _featureFlagProvider = featureFlagProvider;
        _fileContentTypeProvider = fileContentTypeProvider;
        _remoteNodeService = remoteNodeService;
        _linkApiClient = linkApiClient;
        _folderApiClient = folderApiClient;
        _fileApiClient = fileApiClient;
        _photoApiClient = photoApiClient;
        _sdkClientFactory = sdkClientFactory;
        _cryptographyService = cryptographyService;
        _loggerFactory = loggerFactory;

        _recordMetric = metricsRecorder.Record;
    }

    public IFileSystemClient<string> CreateClient(FileSystemClientParameters parameters)
    {
        if (parameters.IsPhotoClient)
        {
            return CreateLegacyClient(parameters);
        }

        // The hybrid client will selectively use the legacy and the SDK client based on feature flags per operation
        var legacyClient = CreateLegacyClient(parameters);
        var sdkClient = CreateSdkClient(parameters);

        return new HybridRemoteFileSystemClient(_featureFlagProvider, legacyClient, sdkClient);
    }

    private RemoteFileSystemClient CreateLegacyClient(FileSystemClientParameters parameters)
    {
        return new RemoteFileSystemClient(
            parameters,
            _fileContentTypeProvider,
            _remoteNodeService,
            _linkApiClient,
            _folderApiClient,
            _fileApiClient,
            _photoApiClient,
            _cryptographyService,
            _loggerFactory);
    }

    private SdkFileSystemClient CreateSdkClient(FileSystemClientParameters parameters)
    {
        return new SdkFileSystemClient(
            parameters,
            _driveApiConfig,
            _sdkClientFactory.GetOrCreateClient(),
            _fileContentTypeProvider,
            _remoteNodeService,
            _linkApiClient,
            _featureFlagProvider,
            _recordMetric,
            _loggerFactory);
    }
}
