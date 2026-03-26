using Microsoft.Extensions.Logging;
using ProtonDrive.Client.BlockVerification;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Client.FileUploading;
using ProtonDrive.Client.MediaTypes;
using ProtonDrive.Client.RemoteNodes;
using ProtonDrive.Client.Sdk;
using ProtonDrive.Client.Volumes;
using ProtonDrive.Shared.Devices;
using ProtonDrive.Shared.Features;
using ProtonDrive.Shared.Metrics;
using ProtonDrive.Shared.Reporting;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client;

internal sealed class RemoteFileSystemClientFactory : IRemoteFileSystemClientFactory
{
    private readonly DriveApiConfig _driveApi;
    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly IFileContentTypeProvider _fileContentTypeProvider;
    private readonly IClientInstanceIdentityProvider _clientInstanceIdentityProvider;
    private readonly IRemoteNodeService _remoteNodeService;
    private readonly ILinkApiClient _linkApiClient;
    private readonly IFolderApiClient _folderApiClient;
    private readonly IFileApiClient _fileApiClient;
    private readonly IPhotoApiClient _photoApiClient;
    private readonly IVolumeApiClient _volumeApiClient;
    private readonly ISdkClientFactory _sdkClientFactory;
    private readonly ICryptographyService _cryptographyService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRevisionSealerFactory _revisionSealerFactory;
    private readonly IRevisionManifestCreator _revisionManifestCreator;
    private readonly IBlockVerifierFactory _blockVerifierFactory;
    private readonly ILoggerFactory _loggerFactory;

    private readonly Action<MetricEvent> _recordMetric;
    private readonly Action<Exception> _reportIntegrityFailure;

    public RemoteFileSystemClientFactory(
        DriveApiConfig driveApi,
        IFeatureFlagProvider featureFlagProvider,
        IFileContentTypeProvider fileContentTypeProvider,
        IClientInstanceIdentityProvider clientInstanceIdentityProvider,
        IRemoteNodeService remoteNodeService,
        ILinkApiClient linkApiClient,
        IFolderApiClient folderApiClient,
        IFileApiClient fileApiClient,
        IPhotoApiClient photoApiClient,
        IVolumeApiClient volumeApiClient,
        ISdkClientFactory sdkClientFactory,
        ICryptographyService cryptographyService,
        IHttpClientFactory httpClientFactory,
        IRevisionSealerFactory revisionSealerFactory,
        IRevisionManifestCreator revisionManifestCreator,
        IBlockVerifierFactory blockVerifierFactory,
        IErrorReporting errorReporting,
        IMetricsRecorder metricsRecorder,
        ILoggerFactory loggerFactory)
    {
        _driveApi = driveApi;
        _featureFlagProvider = featureFlagProvider;
        _fileContentTypeProvider = fileContentTypeProvider;
        _clientInstanceIdentityProvider = clientInstanceIdentityProvider;
        _remoteNodeService = remoteNodeService;
        _linkApiClient = linkApiClient;
        _folderApiClient = folderApiClient;
        _fileApiClient = fileApiClient;
        _photoApiClient = photoApiClient;
        _volumeApiClient = volumeApiClient;
        _sdkClientFactory = sdkClientFactory;
        _cryptographyService = cryptographyService;
        _httpClientFactory = httpClientFactory;
        _revisionSealerFactory = revisionSealerFactory;
        _revisionManifestCreator = revisionManifestCreator;
        _blockVerifierFactory = blockVerifierFactory;
        _loggerFactory = loggerFactory;

        _recordMetric = metricsRecorder.Record;
        _reportIntegrityFailure = errorReporting.CaptureException;
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
            _driveApi,
            parameters,
            _fileContentTypeProvider,
            _clientInstanceIdentityProvider,
            _remoteNodeService,
            _linkApiClient,
            _folderApiClient,
            _fileApiClient,
            _photoApiClient,
            _volumeApiClient,
            _cryptographyService,
            _httpClientFactory,
            _revisionSealerFactory,
            _revisionManifestCreator,
            _blockVerifierFactory,
            _featureFlagProvider,
            _loggerFactory,
            _reportIntegrityFailure);
    }

    private SdkFileSystemClient CreateSdkClient(FileSystemClientParameters parameters)
    {
        return new SdkFileSystemClient(
            parameters,
            _sdkClientFactory.GetOrCreateClient(),
            _fileContentTypeProvider,
            _remoteNodeService,
            _linkApiClient,
            _featureFlagProvider,
            _recordMetric,
            _reportIntegrityFailure);
    }
}
