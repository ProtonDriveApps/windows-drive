using Microsoft.Extensions.Logging;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Client.Cryptography.Pgp;
using ProtonDrive.Client.Photos.Contracts;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client.FileUploading;

internal sealed class PhotoRevisionSealer : RevisionSealer
{
    private readonly IExtendedAttributesBuilder _extendedAttributesBuilder;
    private readonly IPhotoHashProvider _photoHashProvider;
    private readonly IFileMetadataProvider _fileMetadataProvider;

    private readonly string _parentLinkId;
    private readonly string _shareId;

    public PhotoRevisionSealer(
        RevisionSealerParameters parameters,
        IPgpSignatureProducer signatureProducer,
        Address signatureAddress,
        IRevisionManifestCreator revisionManifestCreator,
        IExtendedAttributesBuilder extendedAttributesBuilder,
        IFileRevisionUpdateApiClient fileRevisionUpdateApiClient,
        IPhotoHashProvider photoHashProvider,
        IFileMetadataProvider fileMetadataProvider,
        ILogger<RevisionSealer> logger)
        : base(
            parameters,
            signatureProducer,
            signatureAddress,
            revisionManifestCreator,
            extendedAttributesBuilder,
            fileRevisionUpdateApiClient,
            logger)
    {
        _parentLinkId = parameters.ParentLinkId ?? throw new ArgumentNullException(nameof(parameters), "ParentLinkId is required");
        _shareId = parameters.ShareId;
        _extendedAttributesBuilder = extendedAttributesBuilder;
        _photoHashProvider = photoHashProvider;
        _fileMetadataProvider = fileMetadataProvider;
    }

    protected override async Task<RevisionUpdateParameters> GetRevisionParametersAsync(
        string manifestSignature,
        string? extendedAttributes,
        RevisionSealingParameters revisionSealingParameters,
        CancellationToken cancellationToken)
    {
        if (revisionSealingParameters is not PhotoRevisionSealingParameters photoRevisionSealingParameters)
        {
            throw new ArgumentException("Photo revision sealing failed: missing photo revision parameters", nameof(revisionSealingParameters));
        }

        var parameters = await base.GetRevisionParametersAsync(
                manifestSignature,
                extendedAttributes,
                revisionSealingParameters,
                cancellationToken)
            .ConfigureAwait(false);

        var contentHash = await _photoHashProvider.GetContentHashAsync(_shareId, _parentLinkId, revisionSealingParameters.Sha1Digest, cancellationToken)
            .ConfigureAwait(false);
        var captureTime = (_extendedAttributesBuilder.CaptureTime ?? photoRevisionSealingParameters.DefaultCaptureTimeUtc).ToUnixTimeSeconds();
        var photoTags = await _fileMetadataProvider.GetPhotoTagsAsync(cancellationToken).ConfigureAwait(false);

        parameters.PhotoDetails = new PhotoRevisionDetails(captureTime, contentHash, photoRevisionSealingParameters.MainPhotoLinkId, photoTags);
        return parameters;
    }
}
